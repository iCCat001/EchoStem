using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Dopamine.Core.Api.GitHub
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
        /// Gets the latest (newest published) release of a repository. Drafts are not returned by
        /// the API, and the releases are sorted by creation date (newest first).
        /// </summary>
        public static async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=20";
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("request");

            var releasesResponse = await httpClient.GetStringAsync(url);
            var releases = JArray.Parse(releasesResponse);

            var latestRelease = releases.FirstOrDefault() as JObject;

            if (latestRelease == null)
            {
                return null;
            }

            var release = new GitHubRelease
            {
                TagName = latestRelease["tag_name"]?.ToString(),
                Name = latestRelease["name"]?.ToString(),
                Body = latestRelease["body"]?.ToString()
            };

            var assets = latestRelease["assets"] as JArray;

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
