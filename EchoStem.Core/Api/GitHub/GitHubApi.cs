using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace EchoStem.Core.Api.GitHub
{
    public class GitHubReleaseAsset
    {
        public string Name { get; set; }

        public string DownloadUrl { get; set; }

        public long Size { get; set; }
    }

    public class GitHubRelease
    {
        public string TagName { get; set; }

        public string Name { get; set; }

        public string Body { get; set; }

        public List<GitHubReleaseAsset> Assets { get; set; } = new List<GitHubReleaseAsset>();
    }

    public static class GitHubApi
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Gets the releases of a repository (newest first, drafts excluded). The GitHub API sorts
        /// releases by their creation date, which is not necessarily the same as their version, so
        /// callers must not rely on the order when looking for the newest version.
        /// </summary>
        public static async Task<List<GitHubRelease>> GetReleasesAsync(string owner, string repo)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=100";
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");

            var releasesResponse = await httpClient.GetStringAsync(url);
            var releases = JArray.Parse(releasesResponse);
            var result = new List<GitHubRelease>();

            foreach (var releaseToken in releases.OfType<JObject>())
            {
                result.Add(ParseRelease(releaseToken));
            }

            return result;
        }

        /// <summary>
        /// Gets the first release returned by the GitHub API. Kept for callers which only need a
        /// simple "latest" lookup; update checks should use <see cref="GetReleasesAsync"/> instead.
        /// </summary>
        public static async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo)
        {
            List<GitHubRelease> releases = await GetReleasesAsync(owner, repo);

            return releases.FirstOrDefault();
        }

        private static GitHubRelease ParseRelease(JObject releaseToken)
        {
            var release = new GitHubRelease
            {
                TagName = releaseToken["tag_name"]?.ToString(),
                Name = releaseToken["name"]?.ToString(),
                Body = releaseToken["body"]?.ToString()
            };

            var assets = releaseToken["assets"] as JArray;

            if (assets != null)
            {
                foreach (var asset in assets.OfType<JObject>())
                {
                    release.Assets.Add(new GitHubReleaseAsset
                    {
                        Name = asset["name"]?.ToString(),
                        DownloadUrl = asset["browser_download_url"]?.ToString(),
                        Size = asset["size"] != null ? asset["size"].Value<long>() : 0
                    });
                }
            }

            return release;
        }
    }
}
