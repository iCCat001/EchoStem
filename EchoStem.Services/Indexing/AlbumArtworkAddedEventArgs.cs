using System;
using System.Collections.Generic;

namespace EchoStem.Services.Indexing
{
    public class AlbumArtworkAddedEventArgs : EventArgs
    {
        public IList<string> AlbumKeys { get; set; }
    }
}