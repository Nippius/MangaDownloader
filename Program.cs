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
        static int maxConcurPageDown;
        static int delayBetweenChapters;
        static int firstChapter;
        static int lastChapter;
        // Path where all the chapters will be stored.
        static string savePath;
        static HttpClient hc;

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

            // Create folder if it dosen´t exist
            savePath = ConfigurationManager.AppSettings["SavePath"];
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            // Chapter URL's start at 1 so we need to add one to get the correct chapter
            // Ex: Chapter 687 is URL http://www.mangastream.to/naruto-chapter-688.html
            firstChapter++;
            lastChapter++;
        }

        static void Main(string[] args)
        {
            Init();

            hc = new HttpClient();

            Download().ContinueWith((x) =>
            {
                Console.WriteLine("All done!");
            });

            // Carregar em qualquer tecla para terminar.
            Console.ReadLine();

            /*
            // Testes
            int chapter = 661;
            int page = 1;

            HttpResponseMessage hrm = hc.GetAsync(url_base+url_img_folder+chapter+"/02"+url_etx).Result;
            HttpContent st = hrm.Content;
            //Console.WriteLine(st.ReadAsStringAsync().Result);
 
            using(FileStream f = File.OpenWrite("/681/teste.png"))
            {
              byte[] res = await st.ReadAsByteArrayAsync();

            f.Write(res, 0, res.Length)
            };
            */

            // ISTO É UM BUG DESCOMUNAL! Estamos infinitamente a criar tasks para fazer download e todas elas vão começar a sacar capitulos desde o incio
            // Exemplo:
            //  t1 - Sacar todos os capitulos de 661 até ao 685
            //  t2 - Sacar todos os capitulos de 661 até ao 685
            // ...
            //while(!Download().IsCompleted); 
        }

        /// <summary>
        /// Downloads all the chapters specified in App.config
        /// </summary>
        /// <returns>Returns a Task that will complete when all chapters are downloaded.</returns>
        private async static Task Download()
        {
            for (int chapter = firstChapter; chapter <= lastChapter; chapter++)
            {
                // Create folder for current chapter
                string destChapterFolder = savePath + "\\" + (chapter - 1).ToString();
                if (!Directory.Exists(destChapterFolder))
                {
                    Directory.CreateDirectory(destChapterFolder);
                }


                // Wait for each chapter to finish downloading
                //Console.Write("Processing chapter: " + (chapter - 1) + " -- ");
                await GetChapter(chapter);
                //Console.WriteLine("Ok!");
                // This should NEVER be used! Must find a better solution.
                await Task.Delay(delayBetweenChapters);
            }
        }

        /// <summary>
        /// Downloads the chapter corresponding to the chapter number provided.
        /// </summary>
        /// <param name="chapter">The chapter number to download.</param>
        /// <returns>Returns a Task that will complete once the chapter is completely downloaded.</returns>
        private static async Task GetChapter(int chapter)
        {
            // Get how many pages the current chapter has
            int totalPageCount = await FetchTotalNumberOfPagesForCurrentChapter(chapter);


            // List of pages currently downloading.
            LinkedList<Task> pagesCurrentlyDownloading = new LinkedList<Task>();

            for (int page = 1; page <= totalPageCount; page++)
            {
                pagesCurrentlyDownloading.AddFirst(GetPage(chapter, page));

                if (pagesCurrentlyDownloading.Count == maxConcurPageDown)
                {
                    // Once we are at the limit, wait for at least one to finish
                    await Task.WhenAny(pagesCurrentlyDownloading.ToArray()).ContinueWith((x) =>
                    {
                        // WhenAny() returns the instance of Task that ended so we can remove it from
                        // the queue of pages and add another one. NEEDS MORE TESTING
                        // in order to garantee that there is no concurrency problems. (does it
                        // need locks or a synchronizer?)
                        pagesCurrentlyDownloading.Remove(x);
                    });
                }
            }
        }

        /// <summary>
        /// Given the current chapter number, this functions query's the website
        /// for the total page count.
        /// </summary>
        /// <param name="chapter">Chapter number to query the total page count</param>
        /// <returns>Returns the total page count for the given chapter.</returns>
        private static async Task<int> FetchTotalNumberOfPagesForCurrentChapter(int chapter)
        {
            // Note, all this urls are valid:
            // http://www.mangastream.to/naruto-chapter-663.html
            // http://www.mangastream.to/naruto-chapter-663-page-0.html
            // http://www.mangastream.to/naruto-chapter-663-page-1.html
            // All of them return the first page of the chapter.
            // For convenience we will use http://www.mangastream.to/naruto-chapter-{0}-page-0.html
            // since that URL is already defined in App.config.
            string url = String.Format(urlBase, chapter, 0);

            HttpResponseMessage hrm = await hc.GetAsync(url);
            if (hrm.StatusCode != HttpStatusCode.OK)
                throw new FullImageURLNotFoundException();

            HtmlDocument hdoc = new HtmlDocument();
            hdoc.Load(await hrm.Content.ReadAsStreamAsync());

            if (hdoc.DocumentNode != null)
            {
                foreach (HtmlNode link in hdoc.DocumentNode.SelectNodes("//select"))
                {
                    HtmlAttribute attribute = link.Attributes["name"];
                    if (attribute != null)
                    {
                        if (attribute.Value.Equals("pages"))
                        {
                            int total = 0;
                            // This is stupid. For now this is the only way i found to calculate how many 
                            // pages a given chapter has...
                            foreach(HtmlNode childNode in link.ChildNodes)
                            {
                                if(childNode.Name.Equals("option"))
                                {
                                    total = Convert.ToInt32(childNode.Attributes["value"].Value);
                                }
                            }
                            return total;
                        }
                    }
                }
            }
            throw new TotalPageCountException();
        }

        /// <summary>
        /// Downloads asynchronously a single page from a chapter of the manga specified.
        /// </summary>
        /// <param name="chapter">The chapter number tha the page corresponds to.</param>
        /// <param name="page">The page number to download.</param>
        /// <returns>Returns a Task that will complete once the page is completely downloaded.</returns>
        private static async Task GetPage(int chapter, int page)
        {
            string fullImageUrl;
            try
            {
                fullImageUrl = await FetchtFullImageURL(chapter, page);
            }catch(FullImageURLNotFoundException)
            {
                Console.WriteLine("Chapter {0} page {1} could not be retrived.", chapter, page);
                return;
            }
            
            HttpResponseMessage hrm = await hc.GetAsync(fullImageUrl);
            if(hrm.StatusCode == HttpStatusCode.OK)
            {
                HttpContent st = hrm.Content;
                //Console.WriteLine(st.ReadAsStringAsync().Result);
                StringBuilder destFile = new StringBuilder(savePath);
                destFile.Append("\\");
                destFile.Append((chapter - 1));
                destFile.Append("\\");
                destFile.Append(page);
                destFile.Append(fullImageUrl.Substring(fullImageUrl.Length-4)); // File extension

                // c:\naruto\1.png
                using (FileStream f = File.OpenWrite(destFile.ToString()))
                {
                  byte[] res = await st.ReadAsByteArrayAsync();

                  f.Write(res, 0, res.Length); // Write the file
                };

                Console.WriteLine("Chapter {0} page {1}: Ok!", chapter, page);
            }
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
        private static async Task<string> FetchtFullImageURL(int chapter, int page)
        {
            string url = String.Format(urlBase, chapter, page);

            HttpResponseMessage hrm = await hc.GetAsync(url);
            if (hrm.StatusCode != HttpStatusCode.OK)
                throw new FullImageURLNotFoundException();

            HtmlDocument hdoc = new HtmlDocument();
            hdoc.Load(await hrm.Content.ReadAsStreamAsync());

            if (hdoc.DocumentNode != null)
            {
                foreach (HtmlNode link in hdoc.DocumentNode.SelectNodes("//img"))
                {
                    HtmlAttribute attribute = link.Attributes["class"];
                    if (attribute != null)
                    {
                        if (attribute.Value.Equals("manga-page"))
                        {
                            //Console.WriteLine(link.Attributes["src"].Value);
                            return link.Attributes["src"].Value;
                        }
                    }
                }
            }
            throw new FullImageURLNotFoundException();
        }
    }
}
