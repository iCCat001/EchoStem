using System;
using System.Text.RegularExpressions;

namespace Dopamine.Services.Update
{
    /// <summary>
    /// A version which is either parsed from a release tag (e.g. "V2.0.11-Beta3") or from the
    /// assembly version (e.g. "2.0.11.2001"). In the assembly version, the revision encodes the
    /// channel: 1xxx = Preview, 2xxx = Beta (2001 = Beta 1), 3xxx = Release Candidate,
    /// 4000 = Release. Versions are ordered by number, then by channel, then by channel number.
    /// </summary>
    public sealed class AppVersion : IComparable<AppVersion>
    {
        private const int ChannelPreview = 1;
        private const int ChannelBeta = 2;
        private const int ChannelReleaseCandidate = 3;
        private const int ChannelStable = 4;

        private static readonly Regex versionRegex = new Regex(
            @"^(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<build>\d+))?(?:\.(?<revision>\d+))?(?:-(?<label>[A-Za-z]+)[\s_.-]*(?<labelNumber>\d+)?)?$",
            RegexOptions.Compiled);

        public int Major { get; private set; }

        public int Minor { get; private set; }

        public int Build { get; private set; }

        public int Channel { get; private set; }

        public int ChannelNumber { get; private set; }

        public string Text { get; private set; }

        /// <summary>Parses a release tag or a version string.</summary>
        public static AppVersion Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = text.Trim();

            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }

            Match match = versionRegex.Match(text);

            if (!match.Success)
            {
                return null;
            }

            var version = new AppVersion
            {
                Major = ParseGroup(match, "major"),
                Minor = ParseGroup(match, "minor"),
                Build = ParseGroup(match, "build"),
                Text = text
            };

            int revision = ParseGroup(match, "revision");
            string label = match.Groups["label"].Value;
            int labelNumber = ParseGroup(match, "labelNumber");

            if (!string.IsNullOrEmpty(label))
            {
                // Tag form: 2.0.11-Beta3
                version.Channel = GetChannelFromLabel(label);
                version.ChannelNumber = labelNumber;
            }
            else if (revision >= 1000)
            {
                // Assembly form: 2.0.11.2001
                version.Channel = Math.Min(revision / 1000, ChannelStable);
                version.ChannelNumber = revision % 1000;
            }
            else
            {
                // Plain version: 2.0.11
                version.Channel = ChannelStable;
                version.ChannelNumber = 0;
            }

            return version;
        }

        public static AppVersion FromAssemblyVersion(Version version)
        {
            if (version == null)
            {
                return null;
            }

            return Parse(version.ToString());
        }

        public int CompareTo(AppVersion other)
        {
            if (other == null)
            {
                return 1;
            }

            int comparison = this.Major.CompareTo(other.Major);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.Minor.CompareTo(other.Minor);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.Build.CompareTo(other.Build);

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = this.Channel.CompareTo(other.Channel);

            if (comparison != 0)
            {
                return comparison;
            }

            return this.ChannelNumber.CompareTo(other.ChannelNumber);
        }

        public override string ToString()
        {
            string text = $"{this.Major}.{this.Minor}.{this.Build}";

            switch (this.Channel)
            {
                case ChannelPreview:
                    return $"{text}-Preview{this.ChannelNumber}";
                case ChannelBeta:
                    return $"{text}-Beta{this.ChannelNumber}";
                case ChannelReleaseCandidate:
                    return $"{text}-RC{this.ChannelNumber}";
                default:
                    return text;
            }
        }

        private static int GetChannelFromLabel(string label)
        {
            switch (label.ToLowerInvariant())
            {
                case "preview":
                case "alpha":
                    return ChannelPreview;
                case "rc":
                    return ChannelReleaseCandidate;
                case "release":
                case "stable":
                    return ChannelStable;
                default:
                    return ChannelBeta;
            }
        }

        private static int ParseGroup(Match match, string groupName)
        {
            Group group = match.Groups[groupName];

            if (group.Success && int.TryParse(group.Value, out int value))
            {
                return value;
            }

            return 0;
        }
    }
}
