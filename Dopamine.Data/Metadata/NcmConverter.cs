using Dopamine.Core.Audio;
using System;
using System.IO;
using System.Linq;
using TagLib;

namespace Dopamine.Data.Metadata
{
    /// <summary>
    /// Decrypts an NCM file into a normal, playable audio file. The original file name is kept and
    /// only the extension changes (to ".flac" or ".mp3"); the NetEase metadata and cover art are
    /// embedded into the result.
    /// </summary>
    public static class NcmConverter
    {
        /// <summary>
        /// Converts the given NCM file and returns the path of the converted file.
        /// Throws when the target file already exists.
        /// </summary>
        public static string Convert(string ncmPath)
        {
            var ncmFile = new NcmFile(ncmPath);

            string directory = Path.GetDirectoryName(ncmPath);
            string nameWithoutExtension = Path.GetFileNameWithoutExtension(ncmPath);
            string targetPath = Path.Combine(directory, nameWithoutExtension + ncmFile.PayloadExtension);

            if (System.IO.File.Exists(targetPath))
            {
                throw new IOException($"The target file '{targetPath}' already exists.");
            }

            // Decrypt the payload into the target file.
            using (Stream payload = ncmFile.OpenPayload())
            using (var output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                payload.CopyTo(output);
            }

            try
            {
                EmbedMetadata(ncmFile, targetPath);
            }
            catch (Exception)
            {
                // The audio was written successfully; failing to write the tags must not fail the conversion.
            }

            return targetPath;
        }

        private static void EmbedMetadata(NcmFile ncmFile, string targetPath)
        {
            NcmMetadata metadata = ncmFile.Metadata;

            using (TagLib.File file = TagLib.File.Create(targetPath))
            {
                if (!string.IsNullOrWhiteSpace(metadata.Name))
                {
                    file.Tag.Title = metadata.Name;
                }

                if (!string.IsNullOrWhiteSpace(metadata.Album))
                {
                    file.Tag.Album = metadata.Album;
                }

                string[] artists = string.IsNullOrWhiteSpace(metadata.Artist)
                    ? new string[0]
                    : metadata.Artist.Split('/').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();

                if (artists.Length > 0)
                {
                    file.Tag.Performers = artists;
                    file.Tag.AlbumArtists = artists;
                }

                // Recent NetEase versions no longer embed the cover in the NCM file: they store its
                // URL in the metadata instead. Fetch it (at full size) in that case.
                byte[] cover = metadata.Cover;
                string coverMimeType = metadata.CoverMimeType;

                if ((cover == null || cover.Length == 0) && !string.IsNullOrWhiteSpace(metadata.AlbumPicUrl))
                {
                    cover = DownloadImage(metadata.AlbumPicUrl);
                    coverMimeType = DetectMimeType(cover);
                }

                if (cover != null && cover.Length > 0)
                {
                    file.Tag.Pictures = new Picture[]
                    {
                        new Picture
                        {
                            Type = PictureType.FrontCover,
                            MimeType = string.IsNullOrEmpty(coverMimeType) ? "image/jpeg" : coverMimeType,
                            Description = "Cover",
                            Data = cover
                        }
                    };
                }

                file.Save();
            }
        }

        // Downloads the cover image exactly as served by the NetEase CDN (full size, no resizing).
        private static byte[] DownloadImage(string url)
        {
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    return client.DownloadData(url);
                }
            }
            catch (Exception)
            {
                // A missing cover must not fail the conversion.
                return null;
            }
        }

        private static string DetectMimeType(byte[] imageData)
        {
            if (imageData == null || imageData.Length < 8)
            {
                return "image/jpeg";
            }

            bool isPng = imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47;

            return isPng ? "image/png" : "image/jpeg";
        }
    }
}
