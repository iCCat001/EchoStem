using Digimezzo.Foundation.Core.Utils;
using System.Collections.Generic;
using System.Windows.Media;

namespace Dopamine.Services.Utils
{
    /// <summary>
    /// Caches the dominant color of album artwork. The same cover art is decoded by several parts
    /// of the UI (the accent color, the Now Playing background gradient), and decoding it is
    /// expensive, so it is only done once per artwork.
    /// </summary>
    public static class DominantColorCache
    {
        private const int MaxEntries = 32;

        private static readonly object syncRoot = new object();
        private static readonly Dictionary<long, Color> colors = new Dictionary<long, Color>();
        private static readonly Queue<long> insertionOrder = new Queue<long>();

        public static Color Get(byte[] artwork)
        {
            if (artwork == null || artwork.Length == 0)
            {
                return Colors.Transparent;
            }

            // Same key as the cover art change detection: the length of the artwork bytes.
            long key = artwork.LongLength;

            lock (syncRoot)
            {
                if (colors.TryGetValue(key, out Color cachedColor))
                {
                    return cachedColor;
                }
            }

            Color color;

            try
            {
                color = ImageUtils.GetDominantColor(artwork);
            }
            catch
            {
                color = Colors.Transparent;
            }

            lock (syncRoot)
            {
                if (!colors.ContainsKey(key))
                {
                    colors[key] = color;
                    insertionOrder.Enqueue(key);

                    while (insertionOrder.Count > MaxEntries)
                    {
                        colors.Remove(insertionOrder.Dequeue());
                    }
                }
            }

            return color;
        }
    }
}
