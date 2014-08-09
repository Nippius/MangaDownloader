using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaDownloader.Exceptions
{
    class TotalPageCountException : Exception
    {
        public TotalPageCountException() : base("Couldn´t calculate how many pages the chater has.")
        {
            
        }
    }
}
