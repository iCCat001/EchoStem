using System;
using System.Text;

namespace Dopamine.Core.Base
{
    /// <summary>
    /// Password check which unlocks the non-public, experimental features. The password is never
    /// stored in clear text: only a lightly obfuscated form is kept, so it can't be read straight
    /// out of the compiled assembly.
    /// </summary>
    public static class AccessControl
    {
        // The default password, XOR-masked with the mask below and Base64 encoded.
        private const string obfuscatedKey = "DDANCjgtChg=";
        private static readonly byte[] mask = Encoding.UTF8.GetBytes("EchoStem");

        /// <summary>Returns true when the given password unlocks the non-public features.</summary>
        public static bool Verify(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }

            byte[] data = Encoding.UTF8.GetBytes(password);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= mask[i % mask.Length];
            }

            return string.Equals(Convert.ToBase64String(data), obfuscatedKey, StringComparison.Ordinal);
        }
    }
}
