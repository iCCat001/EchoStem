using System;

namespace EchoStem.Services.Metadata
{
    public class RatingChangedEventArgs : EventArgs
    {
        public string SafePath { get; }
        public int Rating { get; }

        public RatingChangedEventArgs(string safePath, int rating)
        {
            this.SafePath = safePath;
            this.Rating = rating;
        }
    }
}
