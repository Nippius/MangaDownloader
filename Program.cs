using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Configuration;
using System.Net;
using System.Threading;
using HtmlAgilityPack;
using MangaDownloader.Exceptions;

namespace MangaDownloader
{
    class Program
    {
        // Maximum number of pages being downloaded
        static string urlBase;
        // Unused for now..
        static int maxConcurPageDown;
        static int delayBetweenChapters;
        static int firstChapter;
        static int lastChapter;
        // Path where all the chapters will be stored.
        static string savePath;
        static HttpClient hc;

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Unused</param>
        static void Main()
        {
            Init();

            hc = new HttpClient();

            DownloadAsync().Wait();
        }

        /// <summary>
        /// Initializes all the necessary variables by reading the values from App.config
        /// </summary>
        static void Init()
        {
            maxConcurPageDown = Convert.ToInt32(ConfigurationManager.AppSettings["maxConcurrentPageDownloads"]);
            delayBetweenChapters = Convert.ToInt32(ConfigurationManager.AppSettings["delayBetweenChapters"]);
            urlBase = ConfigurationManager.AppSettings["urlBase"];
            firstChapter = Convert.ToInt32(ConfigurationManager.AppSettings["startChapter"]);
            lastChapter = Convert.ToInt32(ConfigurationManager.AppSettings["endChapter"]);

            savePath = ConfigurationManager.AppSettings["SavePath"];
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
        }

        /// <summary>
        /// Downloads all the chapters specified in App.config
        /// </summary>
        /// <returns>Returns a Task that will complete when all chapters are downloaded.</returns>
        static async Task DownloadAsync()
        {
            // Chapters start at 1 so we need to add 1
            Console.WriteLine("Downloading {0} chapter(s).", (lastChapter - firstChapter) + 1);

            for (int chapter = firstChapter; chapter <= lastChapter; chapter++)
            {
                Console.Write("Chapter {0}: ", chapter);

                // Create folder for current chapter if needed
                string destChapterFolder = savePath + "\\" + Convert.ToString(chapter);
                if (!Directory.Exists(destChapterFolder))
                {
                    Directory.CreateDirectory(destChapterFolder);
                }

                await GetChapterAsync(chapter);

                Console.WriteLine("Complete!");

                // No need to wait if it is the last chapter
                if (chapter != lastChapter)
                {
                    // This should NEVER be used! Must find a better solution.
                    Thread.Sleep(delayBetweenChapters);
                }
            }
        }

        /// <summary>
        /// Downloads the chapter corresponding to the chapter number provided.
        /// </summary>
        /// <param name="chapter">The chapter number to download.</param>
        /// <returns>Returns a Task that will complete once the chapter is completely downloaded.</returns>
        static async Task GetChapterAsync(int chapter)
        {
            int totalPageCount;
            try
            {
                totalPageCount = await FetchTotalNumberOfPagesForCurrentChapterAsync(chapter);
            }
            catch (TotalPageCountException tpce)
            {
                Console.WriteLine(tpce.Message);
                Console.WriteLine("Unable to find how many pages chapter {0} has!", chapter);
                return;
            }

            // List of pages currently downloading.
            LinkedList<Task> pagesCurrentlyDownloading = new LinkedList<Task>();

            for (int page = 1; page <= totalPageCount; page++)
            {
                pagesCurrentlyDownloading.AddFirst(GetPageAsync(chapter, page));

                // Must find a way to throttle the number of downloaded pages
                // in order to avoid overloading the server and getting our ip
                // banned...
            }

            await Task.WhenAll(pagesCurrentlyDownloading.ToArray());
        }

        /// <summary>
        /// Downloads asynchronously a single page from a chapter of the manga specified.
        /// </summary>
        /// <param name="chapter">The chapter number tha the page corresponds to.</param>
        /// <param name="page">The page number to download.</param>
        /// <returns>Returns a Task that will complete once the page is completely downloaded.</returns>
        static async Task GetPageAsync(int chapter, int page)
        {
            string fullImageUrl;
            try
            {
                fullImageUrl = await FetchtFullImageURLAsync(chapter, page);
            }
            catch (FullImageURLNotFoundException fiunfe)
            {
                Console.WriteLine(fiunfe.Message);
                Console.WriteLine("Chapter {0} page {1} could not be retrived.", chapter, page);
                return;
            }

            // Download the page
            HttpResponseMessage hrm = await hc.GetAsync(fullImageUrl);
            if (hrm.StatusCode == HttpStatusCode.OK)
            {
                HttpContent imageData = hrm.Content;

                // Build the path to the file where to write the image data
                StringBuilder destFile = new StringBuilder(savePath);
                destFile.Append("\\");
                destFile.Append(chapter);
                destFile.Append("\\");
                destFile.Append(page);
                destFile.Append(fullImageUrl.Substring(fullImageUrl.Length - 4)); // File extension

                // Save to file
                using (FileStream f = File.OpenWrite(destFile.ToString()))
                {
                    byte[] res = await imageData.ReadAsByteArrayAsync();

                    f.Write(res, 0, res.Length);
                };
            }
        }

        /// <summary>
        /// Given the current chapter number, this functions query's the website
        /// for the total page count.
        /// </summary>
        /// <param name="chapter">Chapter number to query the total page count</param>
        /// <returns>Returns the total page count for the given chapter.</returns>
        static async Task<int> FetchTotalNumberOfPagesForCurrentChapterAsync(int chapter)
        {
            if (chapter < 1)
            {
                throw new TotalPageCountException("Invalid chapter number!");
            }

            // Note, all this urls are valid:
            // http://www.mangastream.to/naruto-chapter-663.html
            // http://www.mangastream.to/naruto-chapter-663-page-0.html
            // http://www.mangastream.to/naruto-chapter-663-page-1.html
            // All of them return the first page of the chapter.
            // For convenience we will use http://www.mangastream.to/naruto-chapter-{0}-page-0.html
            // since that URL is already defined in App.config (urlBase).
            // We also need to add 1 to the chapter number because chapters start
            // at 1 and not at 0 (there is no chapter 0)
            string url = String.Format(urlBase, chapter + 1, 0);

            HttpResponseMessage hrm = await hc.GetAsync(url);
            if (hrm.StatusCode != HttpStatusCode.OK)
            {
                throw new TotalPageCountException("Unable to contact the server in order the retrieve the total page count!");
            }

            HtmlDocument hdoc = new HtmlDocument();
            hdoc.Load(await hrm.Content.ReadAsStreamAsync());

            // Parse the HTML tree to find the tag "select" in wich the value of the
            // "name" attribute is "pages".
            // Once found, return the value of the last "option" tag.
            if (hdoc.DocumentNode != null)
            {
                foreach (HtmlNode link in hdoc.DocumentNode.SelectNodes("//select"))
                {
                    HtmlAttribute attribute = link.Attributes["name"];
                    if (attribute != null)
                    {
                        if (attribute.Value.Equals("pages"))
                        {
                            int lastPage = 0;
                            // This is stupid. For now this is the only way i found to calculate how many 
                            // pages a given chapter has...
                            foreach (HtmlNode childNode in link.ChildNodes)
                            {
                                if (childNode.Name.Equals("option"))
                                {
                                    lastPage = Convert.ToInt32(childNode.Attributes["value"].Value);
                                }
                            }
                            return lastPage;
                        }
                    }
                }
            }

            throw new TotalPageCountException("Unable to parse the server response!");
        }

        /// <summary>
        /// This function will query the website in order to get the real link to the image.
        /// </summary>
        /// <param name="chapter">Chapter to search.</param>
        /// <param name="page">Page to find.</param>
        /// <returns>The full URL of the image</returns>
        /// <exception cref="FullImageURLNotFoundException">
        /// Throws FullImageURLNotFoundException if the image URL cannot be retrived.
        /// </exception>
        /// <remarks>
        /// We need this because there is no API to know what the extension of the image is,
        /// or if it is double image. The image url can have the following patterns:
        /// XX.jpg
        /// XX.png
        /// XX-YY.png
        /// </remarks>
        static async Task<string> FetchtFullImageURLAsync(int chapter, int page)
        {
            if(chapter < 1 || page < 1)
            {
                throw new FullImageURLNotFoundException("Invalid chapter or page number!");
            }

            // We need to add 1 to the chapter number because chapter URL's start
            // at 1 and not at 0 (there is no chapter 0).
            // This is necessarie because we download the chapter page in order
            // to find the correct image URL
            string url = String.Format(urlBase, chapter + 1, page);

            HttpResponseMessage hrm = await hc.GetAsync(url);
            if (hrm.StatusCode != HttpStatusCode.OK)
            {
                throw new FullImageURLNotFoundException("Unable to contact the server in order the retrive the image URL!");
            }

            HtmlDocument hdoc = new HtmlDocument();
            hdoc.Load(await hrm.Content.ReadAsStreamAsync());

            // Parse the HTML tree to find the tag "img" in wich the value of the
            // "class" attribute is "manga-page".
            // If found, return the value of the "src" attribute.
            if (hdoc.DocumentNode != null)
            {
                foreach (HtmlNode link in hdoc.DocumentNode.SelectNodes("//img"))
                {
                    HtmlAttribute attribute = link.Attributes["class"];
                    if (attribute != null)
                    {
                        if (attribute.Value.Equals("manga-page"))
                        {
                            return link.Attributes["src"].Value;
                        }
                    }
                }
            }

            throw new FullImageURLNotFoundException("Unable to parse the server response!");
        }
    }
}
