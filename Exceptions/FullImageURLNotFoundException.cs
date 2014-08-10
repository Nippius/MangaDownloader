using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaDownloader.Exceptions
{
    [Serializable]
    class FullImageURLNotFoundException : Exception
    {
        public FullImageURLNotFoundException()
            : base("FullImageURLNotFoundException: An unexpected error occured!")
        { }

        public FullImageURLNotFoundException(string message)
            : base("FullImageURLNotFoundException: " + message)
        { }
    }
}
