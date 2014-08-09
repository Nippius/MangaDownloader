﻿using System;
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

            Download().Wait();  // Block Main() unti

            //Console.WriteLine("All downloads finished!");
            // Carregar em qualquer tecla para terminar.
            //Console.ReadLine();

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
                // Parse the HTML in order to get the page total.
                // Could be usefulll in order to get all the page links.
                // GetTotalChapterPagesAvailable()

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
                System.Threading.Thread.Sleep(delayBetweenChapters);
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
            await FetchTotalNumberOfPagesForCurrentChapter(chapter);

            // List of pages currently downloading.
            LinkedList<Task> pages = new LinkedList<Task>();

            for (int page = 1; page < 30; page++)
            {
                // Add next page to download.
                pages.AddFirst(GetPage(chapter, page));

                // Transfer no more than maxConcurPageDown pages at once.
                if (pages.Count == maxConcurPageDown)
                {
                    // Once we are at the limit, wait for at least one to finish
                    await Task.WhenAny(pages.ToArray()).ContinueWith((x) =>
                    {
                        // WhenAny() returns the instance of Task that ended so we can remove it from
                        // the queue of pages and add another one. NEEDS MORE TESTING
                        // in order to garantee that there is no concurrency problems. (does it
                        // need locks or a synchronizer?)
                        pages.Remove(x);
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
        private static Task<int> FetchTotalNumberOfPagesForCurrentChapter(int chapter)
        {
            // Note, all this urls are valid:
            // http://www.mangastream.to/naruto-chapter-663.html
            // http://www.mangastream.to/naruto-chapter-663-page-0.html
            // http://www.mangastream.to/naruto-chapter-663-page-1.html
            // All of them return the first page of the chapter.
            // For convenience we will use http://www.mangastream.to/naruto-chapter-{0}-page-0.html
            // since that URL is already defined in App.config.
            string url = String.Format(urlBase, chapter, 0);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Downloads asynchronously a single page from a chapter of the manga specified.
        /// </summary>
        /// <param name="chapter">The chapter number tha the page corresponds to.</param>
        /// <param name="page">The page number to download.</param>
        /// <returns>Returns a Task that will complete once the page is completely downloaded.</returns>
        private static async Task GetPage(int chapter, int page)
        {
            string fullImageUrl = await FetchtFullImageURL(chapter, page);
            Console.WriteLine(fullImageUrl);
            // Delay entre 1 e 6 segundos
            int delay = ((System.DateTime.Now.Millisecond % 5) + 1) * 1000;

            //Simular trabalho...
            //return Task.Delay(delay).ContinueWith((x) =>
            //{
            //    //Console.WriteLine(chapter + " - " + page +" Completed... OK! Delay was: "+delay);
            //});


            //StringBuilder page_url = new StringBuilder();
            //page_url.Append("/");
            //if (page < 10) page_url.Append("0");
            //page_url.Append(page);

            //HttpResponseMessage hrm = await hc.GetAsync(url_base + urlImageFolder + "/" + manga + "/" + chapter + page_url + urlExtension);
            //if(hrm.StatusCode == HttpStatusCode.OK)
            //{
            //    HttpContent st = hrm.Content;
            //    Console.WriteLine(st.ReadAsStringAsync().Result);

            //    string destFolder = savePath +"\\"+ (chapter - 1).ToString();
            //    // c:\naruto\1.png
            //    using(FileStream f = File.OpenWrite(destFolder+page+urlExtension))
            //    {
            //      byte[] res = await st.ReadAsByteArrayAsync();

            //    f.Write(res, 0, res.Length) // Write the file
            //    };

            //    Console.WriteLine("Chapter {0} page {1}: Ok!", chapter, page);
            //}
        }

        /// <summary>
        /// This function will query the website in order to get the real link to the image.
        /// </summary>
        /// <param name="chapter">Chapter to search.</param>
        /// <param name="page">Page to find.</param>
        /// <returns></returns>
        /// <remarks>
        /// We need this because there is no API to know what the extension of the image is,
        /// or if it is double image. The image url can have the following patterns:
        /// XX.jpg
        /// XX.png
        /// XX-YY.png
        /// </remarks>
        private static async Task<string> FetchtFullImageURL(int chapter, int page)
        {
            string downloadString = String.Format(urlBase, 663, 3);

            HttpResponseMessage hrm = await hc.GetAsync(downloadString);
            if (hrm.StatusCode != HttpStatusCode.OK)
                return "";

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
            // return empty string in case of error.
            return "";
        }
    }
}
