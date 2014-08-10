using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaDownloader.Exceptions
{
    [Serializable]
    class TotalPageCountException : Exception
    {
        public TotalPageCountException()
            : base("TotalPageCountException: An unexpected error occured!")
        { }

        public TotalPageCountException(string message)
            : base("TotalPageCountException: " + message)
        { }
    }
}
