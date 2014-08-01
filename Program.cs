using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Configuration;
using System.Net;

namespace Crawler
{
    class Program
    {
        private static int maxConcurPageDown;  // Maximum number of pages being downloaded
        private static int delayBetweenChapters;
        private static string urlBase;
        private static string urlImageFolder;
        private static string urlManga;
        private static string urlExtension;
        private static int firstChapter;
        private static int lastChapter;

        private static string savePath; // Path where all the chapter will be stored.
        private static HttpClient hc = null;

        static void Main(string[] args)
        {
            maxConcurPageDown = Convert.ToInt32(ConfigurationManager.AppSettings["maxConcurrentPageDownloads"]);
            delayBetweenChapters = Convert.ToInt32(ConfigurationManager.AppSettings["delayBetweenChapters"]);
            urlBase = ConfigurationManager.AppSettings["urlBase"];
            urlImageFolder = ConfigurationManager.AppSettings["urlImageFolder"];
            urlManga = ConfigurationManager.AppSettings["urlManga"];
            urlExtension = ConfigurationManager.AppSettings["urlExtension"];
            firstChapter = Convert.ToInt32(ConfigurationManager.AppSettings["startChapter"]);
            lastChapter = Convert.ToInt32(ConfigurationManager.AppSettings["endChapter"]);

            // Create folder if it dosen´t exist
            savePath = ConfigurationManager.AppSettings["SavePath"]+urlManga;
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            // Chapter URL's start at 1 so we need to add one to get the correct chapter
            // Ex: Chapter 687 is URL http://www.mangastream.to/naruto-chapter-688.html
            firstChapter++;
            lastChapter++;

            hc = new HttpClient();

            Download().Wait();  // Bloquear o main até termos acabado de sacar tudo.

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
                string destChapterFolder = savePath +"\\"+ (chapter - 1).ToString();
                if (!Directory.Exists(destChapterFolder))
                {
                    Directory.CreateDirectory(destChapterFolder);
                }


                // Wait for each chapter to finish downloading
                Console.Write("Processing chapter: "+(chapter-1)+" -- ");
                await GetChapter(chapter);
                Console.WriteLine("Ok!");
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
        /// 
        /// </summary>
        /// <param name="chapter"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        private static Task GetPage(int chapter, int page)
        {

            // Delay entre 1 e 6 segundos
            int delay = ((System.DateTime.Now.Millisecond % 5) + 1) * 1000;

            //Simular trabalho...
            return Task.Delay(delay).ContinueWith((x) =>
            {
                 //Console.WriteLine(chapter + " - " + page +" Completed... OK! Delay was: "+delay);
            });

            /*
            StringBuilder page_url = new StringBuilder();
            page_url.Append("/");
            if (page < 10) page_url.Append("0");
            page_url.Append(page);

            Console.WriteLine(url_base + url_img_folder + chapter + page_url + url_etx);

            HttpResponseMessage hrm = await hc.GetAsync(url_base + urlImageFolder + urlManga + "/" + chapter + page_url + urlExtension);
            if(hrm.StatusCode == HttpStatusCode.OK)
            {
                HttpContent st = hrm.Content;
                Console.WriteLine(st.ReadAsStringAsync().Result);

                string destFolder = savePath +"\\"+ (chapter - 1).ToString();
                // c:\naruto\1.png
                using(FileStream f = File.OpenWrite(destFolder+page+urlExtension))
                {
                  byte[] res = await st.ReadAsByteArrayAsync();

                f.Write(res, 0, res.Length) // Write the file
                };

                Console.WriteLine("Chapter {0} page {1}: Ok!", chapter, page);
            }
            */
        }
    }
}
