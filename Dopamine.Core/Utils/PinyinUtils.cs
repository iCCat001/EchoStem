using System.Text;

namespace Dopamine.Core.Utils
{
    /// <summary>
    /// Determines the pinyin initial of Chinese characters, so that they can be sorted and grouped
    /// together with Latin characters (under A-Z) instead of all ending up under "#".
    /// </summary>
    public static class PinyinUtils
    {
        // The GB2312 code of the first character of each pinyin initial. Chinese has no "i", "u"
        // or "v" initial, which is why those letters are missing from this table.
        private static readonly string[] Initials = { "a", "b", "c", "d", "e", "f", "g", "h", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "w", "x", "y", "z" };
        private static readonly int[] Codes = { 0xB0A1, 0xB0C5, 0xB2C1, 0xB4EE, 0xB6EA, 0xB7A2, 0xB8C1, 0xB9FE, 0xBBF7, 0xBFA6, 0xC0AC, 0xC2E8, 0xC4C3, 0xC5B6, 0xC5BE, 0xC6DA, 0xC8BB, 0xC8F6, 0xCBFA, 0xCDDA, 0xCEF4, 0xD1B9, 0xD4D1 };

        private static readonly Encoding gb2312 = GetGb2312Encoding();

        private static Encoding GetGb2312Encoding()
        {
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the lowercase pinyin initial ("a".."z") of the first character of the given text,
        /// or null when it is not a (GB2312) Chinese character.
        /// </summary>
        public static string GetPinyinInitial(string text)
        {
            if (string.IsNullOrEmpty(text) || gb2312 == null)
            {
                return null;
            }

            char character = text[0];

            // Only try for CJK unified ideographs; Latin and other characters are left untouched.
            if (character < 0x4E00 || character > 0x9FFF)
            {
                return null;
            }

            byte[] bytes;

            try
            {
                bytes = gb2312.GetBytes(new[] { character });
            }
            catch
            {
                return null;
            }

            // Characters outside GB2312 can't be encoded to two bytes: they are not handled here.
            if (bytes.Length != 2)
            {
                return null;
            }

            int code = (bytes[0] & 0xFF) << 8 | (bytes[1] & 0xFF);

            if (code < Codes[0])
            {
                return null;
            }

            string initial = null;

            for (int i = 0; i < Codes.Length; i++)
            {
                if (code >= Codes[i])
                {
                    initial = Initials[i];
                }
                else
                {
                    break;
                }
            }

            return initial;
        }
    }
}
