using Dopamine.Core.Audio;
using Dopamine.Core.Base;
using Dopamine.Core.Extensions;
using System;
using System.Linq;
using TagLib;

namespace Dopamine.Data.Metadata
{
    public class FileMetadata
    {
        private TagLib.File file;
        private NcmMetadata ncmMetadata;
        private string filePath;
        private MetadataValue title;
        private MetadataValue album;
        private MetadataValue albumArtists;
        private MetadataValue artists;
        private MetadataValue genres;
        private MetadataValue comment;
        private MetadataValue grouping;
        private MetadataValue year;
        private MetadataValue trackNumber;
        private MetadataValue trackCount;
        private MetadataValue discNumber;
        private MetadataValue discCount;
        private MetadataRatingValue rating;
        private MetadataArtworkValue artworkData;
        private MetadataValue lyrics;

        public FileMetadata(string filePath)
        {
            ByteVector.UseBrokenLatin1Behavior = true; // Otherwise Latin1 is used as default, which causes characters in various languages being displayed wrong.

            this.filePath = filePath;

            // NCM files are encrypted containers: TagLib can't read them. Their metadata and
            // artwork live in the (decryptable) NetEase header, so read those instead.
            if (NcmFile.IsNcmFile(filePath))
            {
                this.ncmMetadata = new NcmFile(filePath).Metadata;
            }
            else
            {
                this.file = TagLib.File.Create(filePath);
            }
        }

        /// <summary>True for container formats (NCM) whose tags can't be written back to the file.</summary>
        private bool IsNcm => this.ncmMetadata != null;

        /// <summary>True when metadata can be written back to the file on disk.</summary>
        public bool SupportsWriting => !this.IsNcm;

        public string Path => this.file != null ? this.file.Name : this.filePath;

        public string SafePath => this.Path.ToSafePath();

        public int BitRate
        {
            get
            {
                if (this.IsNcm)
                {
                    return this.ncmMetadata.Bitrate;
                }

                // Workaround for a bug in taglibsharp. The Duration field  
                // must be set before the correct AudioBitrate is returned.
                TimeSpan dummy = this.file.Properties.Duration;
                return this.file.Properties.AudioBitrate;
            }
        }

        public int SampleRate => this.IsNcm ? 44100 : this.file.Properties.AudioSampleRate;

        public TimeSpan Duration => this.IsNcm ? TimeSpan.FromMilliseconds(this.ncmMetadata.Duration) : this.file.Properties.Duration;

        public string Type => this.IsNcm
            ? (string.IsNullOrEmpty(this.ncmMetadata.Format) ? "NCM" : this.ncmMetadata.Format.ToUpper())
            : (!string.IsNullOrEmpty(this.file.MimeType) && this.file.MimeType.Split('/').Count() > 1 ? this.file.MimeType.Split('/')[1].ToUpper() : string.Empty);

        public string MimeType => this.IsNcm
            ? "audio/" + (string.IsNullOrEmpty(this.ncmMetadata.Format) ? "ncm" : this.ncmMetadata.Format.ToLower())
            : this.file.MimeType;

        public MetadataValue Title
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(this.ncmMetadata.Name);
                if (this.title == null) this.title = new MetadataValue(this.file.Tag.Title);

                return this.title;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.title = value;
                    this.file.Tag.Title = value.Value;
                }
            }
        }

        public MetadataValue Album
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(this.ncmMetadata.Album);
                if (this.album == null) this.album = new MetadataValue(this.file.Tag.Album);
                return this.album;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.album = value;
                    this.file.Tag.Album = value.Value;
                }
            }
        }

        public MetadataValue AlbumArtists
        {
            get
            {
                if (this.IsNcm) return this.GetNcmArtists();
                if (this.albumArtists == null) this.albumArtists = new MetadataValue(this.file.Tag.AlbumArtists);
                return this.albumArtists;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.albumArtists = value;
                    this.file.Tag.AlbumArtists = value.Values;
                }
            }
        }

        public MetadataValue Artists
        {
            get
            {
                if (this.IsNcm) return this.GetNcmArtists();
                if (this.artists == null) this.artists = new MetadataValue(this.file.Tag.Performers);
                return this.artists;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.artists = value;
                    this.file.Tag.Performers = value.Values;
                }
            }
        }

        public MetadataValue Genres
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.genres == null)
                    this.genres = new MetadataValue(this.file.Tag.Genres);
                return this.genres;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.genres = value;
                    this.file.Tag.Genres = value.Values;
                }
            }
        }

        public MetadataValue Comment
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.comment == null) this.comment = new MetadataValue(this.file.Tag.Comment);
                return this.comment;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.comment = value;
                    this.file.Tag.Comment = value.Value;
                }
            }
        }

        public MetadataValue Grouping
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.grouping == null) this.grouping = new MetadataValue(this.file.Tag.Grouping);
                return this.grouping;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.grouping = value;
                    this.file.Tag.Grouping = value.Value;
                }
            }
        }

        public MetadataValue Year
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.year == null) this.year = new MetadataValue(this.file.Tag.Year);
                return this.year;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.year = value;
                    this.file.Tag.Year = string.IsNullOrEmpty(value.Value) ? (UInt32)0 : Convert.ToUInt32(value.Value);
                }
            }
        }

        public MetadataValue TrackNumber
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.trackNumber == null) this.trackNumber = new MetadataValue(this.file.Tag.Track);
                return this.trackNumber;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.trackNumber = value;
                    this.file.Tag.Track = string.IsNullOrEmpty(value.Value) ? (UInt32)0 : Convert.ToUInt32(value.Value);
                }
            }
        }

        public MetadataValue TrackCount
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.trackCount == null) this.trackCount = new MetadataValue(this.file.Tag.TrackCount);
                return this.trackCount;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.trackCount = value;
                    this.file.Tag.TrackCount = string.IsNullOrEmpty(value.Value) ? (UInt32)0 : Convert.ToUInt32(value.Value);
                }
            }
        }

        public MetadataValue DiscNumber
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.discNumber == null) this.discNumber = new MetadataValue(this.file.Tag.Disc);
                return this.discNumber;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.discNumber = value;
                    this.file.Tag.Disc = string.IsNullOrEmpty(value.Value) ? (UInt32)0 : Convert.ToUInt32(value.Value);
                }
            }
        }

        public MetadataValue DiscCount
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);
                if (this.discCount == null) this.discCount = new MetadataValue(this.file.Tag.DiscCount);
                return this.discCount;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.discCount = value;
                    this.file.Tag.DiscCount = string.IsNullOrEmpty(value.Value) ? (UInt32)0 : Convert.ToUInt32(value.Value);
                }
            }
        }

        public MetadataRatingValue Rating
        {
            get
            {
                if (this.IsNcm)
                {
                    return new MetadataRatingValue();
                }

                if (System.IO.Path.GetExtension(this.file.Name.ToLower()) == FileFormats.MP3)
                {
                    Tag tag = this.file.GetTag(TagTypes.Id3v2);

                    if (tag != null & this.rating == null)
                    {
                        TagLib.Id3v2.PopularimeterFrame popMFrame;

                        // First, try to get the rating from the default Windows PopM user.
                        popMFrame = TagLib.Id3v2.PopularimeterFrame.Get((TagLib.Id3v2.Tag)tag, Defaults.WindowsPopMUser, true);

                        if (popMFrame != null && popMFrame.Rating > 0)
                        {
                            this.rating = new MetadataRatingValue(this.PopM2StarRating(popMFrame.Rating));
                        }
                        else
                        {
                            // No rating found for the default Windows PopM user. Try for other PopM users.
                            foreach (var user in Defaults.OtherPopMUsers)
                            {
                                popMFrame = TagLib.Id3v2.PopularimeterFrame.Get((TagLib.Id3v2.Tag)tag, user, true);

                                if (popMFrame != null && popMFrame.Rating > 0)
                                {
                                    this.rating = new MetadataRatingValue(this.PopM2StarRating(popMFrame.Rating));
                                    break; // As soon as we found a rating, stop.
                                }
                            }
                        }
                    }
                }

                if (this.rating == null)
                {
                    this.rating = new MetadataRatingValue();
                }

                return this.rating;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.rating = value;

                    if (System.IO.Path.GetExtension(this.file.Name.ToLower()) == FileFormats.MP3)
                    {
                        Tag tag = this.file.GetTag(TagTypes.Id3v2);

                        if (tag != null)
                        {
                            TagLib.Id3v2.PopularimeterFrame popMFrame = TagLib.Id3v2.PopularimeterFrame.Get((TagLib.Id3v2.Tag)tag, Defaults.WindowsPopMUser, true);
                            popMFrame.Rating = this.Star2PopMRating(value.Value);
                        }
                    }
                }
            }
        }

        public MetadataArtworkValue ArtworkData
        {
            get
            {
                if (this.IsNcm)
                {
                    return new MetadataArtworkValue(this.ncmMetadata.Cover);
                }

                if (this.artworkData == null)
                {
                    // TODO: there can be more than 1 picture in a file. Should we loop and get the first non-zero picture?
                    this.artworkData = new MetadataArtworkValue(this.file.Tag.Pictures.Length > 0 && this.file.Tag.Pictures[0].Data != null && this.file.Tag.Pictures[0].Data.Data != null && this.file.Tag.Pictures[0].Data.Data.Length > 0 ? (byte[])this.file.Tag.Pictures[0].Data.Data : null);
                }

                return this.artworkData;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.artworkData = value;

                    if (value.Value == null)
                    {
                        // Remove all pictures
                        this.file.Tag.Pictures = new Picture[] { };
                    }
                    else
                    {
                        var pic = new Picture();
                        pic.Type = PictureType.Other;
                        pic.MimeType = DetectImageMimeType(value.Value);
                        pic.Description = "Cover";
                        pic.Data = value.Value;

                        this.file.Tag.Pictures = new Picture[1] { pic };
                    }
                }
            }
        }

        public MetadataValue Lyrics
        {
            get
            {
                if (this.IsNcm) return new MetadataValue(string.Empty);

                if (this.lyrics == null)
                {
                    this.lyrics = new MetadataValue(this.file.Tag.Lyrics);
                }

                return this.lyrics;
            }
            set
            {
                if (this.IsNcm) return;

                if (value.IsValueChanged)
                {
                    this.lyrics = value;
                    this.file.Tag.Lyrics = value.Value;
                }
            }
        }

        public void Save()
        {
            if (this.IsNcm)
            {
                // NCM files are encrypted containers: their tags can't be written back.
                return;
            }

            try
            {
                this.file.Save();
            }
            catch (Exception)
            {
                throw;
            }
        }

        private MetadataValue GetNcmArtists()
        {
            string[] artists = string.IsNullOrWhiteSpace(this.ncmMetadata.Artist)
                ? new string[0]
                : this.ncmMetadata.Artist.Split('/').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();

            return new MetadataValue(artists);
        }

        // NetEase cover art is often a PNG even though the URL ends in ".jpg", so detect the real
        // type instead of assuming JPEG.
        private static string DetectImageMimeType(byte[] imageData)
        {
            if (imageData != null && imageData.Length >= 4 && imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            {
                return "image/png";
            }

            return "image/jpeg";
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !GetType().Equals(obj.GetType()))
            {
                return false;
            }

            return this.SafePath.Equals(((FileMetadata)obj).SafePath);
        }

        public override int GetHashCode()
        {
            return new { this.SafePath }.GetHashCode();
        }

        private byte Star2PopMRating(int rating)
        {

            // 5 stars = POPM 255
            // 4 stars = POPM 196
            // 3 stars = POPM 128
            // 2 stars = POPM 64
            // 1 stars = POPM 1
            // 0 stars = POPM 0

            switch (rating)
            {
                case 0:
                    return 0;
                case 1:
                    return 1;
                case 2:
                    return 64;
                case 3:
                    return 128;
                case 4:
                    return 196;
                case 5:
                    return 255;
                default:
                    // Should not happen
                    return 0;
            }
        }

        private int PopM2StarRating(byte popMRating)
        {

            // 0 stars = POPM 0
            // 1 stars = POPM 1
            // 2 stars = POPM 64
            // 3 stars = POPM 128
            // 4 stars = POPM 196
            // 5 stars = POPM 255

            if (popMRating <= 0)
            {
                return 0;
            }
            else if (popMRating <= 1)
            {
                return 1;
            }
            else if (popMRating <= 64)
            {
                return 2;
            }
            else if (popMRating <= 128)
            {
                return 3;
            }
            else if (popMRating <= 196)
            {
                return 4;
            }
            else if (popMRating <= 255)
            {
                return 5;
            }
            else
            {
                return 0;
                // Should not happen
            }
        }
    }
}
