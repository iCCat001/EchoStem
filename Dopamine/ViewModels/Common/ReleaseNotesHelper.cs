using System.Text.RegularExpressions;

namespace Dopamine.ViewModels.Common
{
    /// <summary>
    /// Turns the markdown body of a GitHub release into plain text: images and links are removed,
    /// only the text is kept.
    /// </summary>
    public static class ReleaseNotesHelper
    {
        private static readonly Regex imageRegex = new Regex(@"!\[[^\]]*\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex linkRegex = new Regex(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex htmlCommentRegex = new Regex(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex headingRegex = new Regex(@"^[ \t]*#{1,6}[ \t]*", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex emphasisRegex = new Regex(@"(\*\*|__|\*|_|`)", RegexOptions.Compiled);

        public static string GetText(string releaseBody)
        {
            if (string.IsNullOrWhiteSpace(releaseBody))
            {
                return string.Empty;
            }

            string text = htmlCommentRegex.Replace(releaseBody, string.Empty);
            text = imageRegex.Replace(text, string.Empty);
            text = linkRegex.Replace(text, "$1");
            text = headingRegex.Replace(text, string.Empty);
            text = emphasisRegex.Replace(text, string.Empty);

            // Collapse the extra blank lines which removing the images can leave behind.
            text = Regex.Replace(text, @"(\r?\n){3,}", "\r\n\r\n");

            return text.Trim();
        }
    }
}
