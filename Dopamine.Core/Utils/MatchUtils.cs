using System.Text;

namespace Dopamine.Core.Utils
{
    /// <summary>
    /// Helpers used to compare text coming from online services (song titles, artists, ...) with
    /// local metadata, in a forgiving way.
    /// </summary>
    public static class MatchUtils
    {
        /// <summary>
        /// Lowercases and removes whitespace, so that e.g. "Song " and "song" compare equal.
        /// </summary>
        public static string NormalizeText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);

            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }
    }
}
