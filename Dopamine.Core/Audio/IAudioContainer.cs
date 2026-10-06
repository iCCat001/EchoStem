using System.IO;

namespace Dopamine.Core.Audio
{
    /// <summary>
    /// A wrapper file format (for example NetEase NCM) which hides a normal, already encoded
    /// audio stream inside it. Supporting a new such format only requires implementing this
    /// interface and registering it in <see cref="NAudioDecoderFactory"/>: nothing else in the
    /// playback pipeline needs to change.
    /// </summary>
    public interface IAudioContainer
    {
        /// <summary>Returns true when the file is this container's format.</summary>
        bool IsContainer(string filename);

        /// <summary>
        /// Opens the wrapped audio as a plain, seekable stream and reports the extension of the
        /// payload (for example ".flac" or ".mp3") so that it can be decoded normally.
        /// </summary>
        Stream OpenPayload(string filename, out string payloadExtension);
    }
}
