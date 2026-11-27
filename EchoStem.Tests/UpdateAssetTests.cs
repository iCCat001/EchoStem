using EchoStem.Core.Api.GitHub;
using EchoStem.Services.Update;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace EchoStem.Tests
{
    [TestFixture]
    public class UpdateAssetTests
    {
        [Test]
        public void FindsEchoStemPortableAndInstallerAssets()
        {
            GitHubRelease release = CreateRelease(
                "EchoStem-Portable-2.0.12-Beta1.zip",
                "EchoStem-Setup-2.0.12-Beta1.exe");

            Assert.That(UpdateService.FindPortableAsset(release).Name, Is.EqualTo("EchoStem-Portable-2.0.12-Beta1.zip"));
            Assert.That(UpdateService.FindInstallerAsset(release).Name, Is.EqualTo("EchoStem-Setup-2.0.12-Beta1.exe"));
        }

        [Test]
        public void FindsLegacyDopamineAssets()
        {
            // The latest release still carries the old Dopamine asset names until the next release
            // is published with the EchoStem names.
            GitHubRelease release = CreateRelease(
                "Dopamine-Portable-2.0.11-Beta9.zip",
                "Dopamine-Setup-2.0.11-Beta9.exe");

            Assert.That(UpdateService.FindPortableAsset(release).Name, Is.EqualTo("Dopamine-Portable-2.0.11-Beta9.zip"));
            Assert.That(UpdateService.FindInstallerAsset(release).Name, Is.EqualTo("Dopamine-Setup-2.0.11-Beta9.exe"));
        }

        [Test]
        public void PrefersEchoStemAssetsOverLegacyOnes()
        {
            GitHubRelease release = CreateRelease(
                "Dopamine-Portable-2.0.11-Beta9.zip",
                "Dopamine-Setup-2.0.11-Beta9.exe",
                "EchoStem-Portable-2.0.12-Beta1.zip",
                "EchoStem-Setup-2.0.12-Beta1.exe");

            Assert.That(UpdateService.FindPortableAsset(release).Name, Is.EqualTo("EchoStem-Portable-2.0.12-Beta1.zip"));
            Assert.That(UpdateService.FindInstallerAsset(release).Name, Is.EqualTo("EchoStem-Setup-2.0.12-Beta1.exe"));
        }

        [Test]
        public void PortablePackageFolderSupportsBothLayouts()
        {
            string root = Path.Combine(Path.GetTempPath(), "EchoStemUpdateTest_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(Path.Combine(root, "EchoStem"));
                Assert.That(UpdateService.GetApplicationFolderFromExtractedPackage(root),
                    Is.EqualTo(Path.Combine(root, "EchoStem")));

                Directory.Delete(Path.Combine(root, "EchoStem"));
                Directory.CreateDirectory(Path.Combine(root, "Dopamine"));
                Assert.That(UpdateService.GetApplicationFolderFromExtractedPackage(root),
                    Is.EqualTo(Path.Combine(root, "Dopamine")));

                Directory.Delete(Path.Combine(root, "Dopamine"));
                Assert.That(UpdateService.GetApplicationFolderFromExtractedPackage(root), Is.EqualTo(root));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SelectsNewestReleaseWithAssetsRegardlessOfApiOrder()
        {
            // The GitHub API returns releases in creation order: here the old release comes first.
            var releases = new List<GitHubRelease>
            {
                CreateReleaseWithTag("V2.0.11-Beta9", "EchoStem-Portable-2.0.11-Beta9.zip", "EchoStem-Setup-2.0.11-Beta9.exe"),
                CreateReleaseWithTag("V2.0.12-Beta1", "EchoStem-Portable-2.0.12-Beta1.zip", "EchoStem-Setup-2.0.12-Beta1.exe")
            };

            UpdateService.UpdateReleaseSelection selection =
                UpdateService.SelectUpdateRelease(releases, AppVersion.Parse("V2.0.11-Beta8"));

            Assert.That(selection.Release.TagName, Is.EqualTo("V2.0.12-Beta1"));
            Assert.That(selection.PortableAsset.Name, Is.EqualTo("EchoStem-Portable-2.0.12-Beta1.zip"));
            Assert.That(selection.InstallerAsset.Name, Is.EqualTo("EchoStem-Setup-2.0.12-Beta1.exe"));
            Assert.That(selection.HasNewerReleaseWithoutPackage, Is.False);
        }

        [Test]
        public void SkipsNewerReleaseWithoutPackageAndUsesNextOneWithAssets()
        {
            var releases = new List<GitHubRelease>
            {
                CreateReleaseWithTag("V2.0.12-Beta2"), // published, but assets were removed
                CreateReleaseWithTag("V2.0.12-Beta1", "EchoStem-Portable-2.0.12-Beta1.zip")
            };

            UpdateService.UpdateReleaseSelection selection =
                UpdateService.SelectUpdateRelease(releases, AppVersion.Parse("V2.0.11-Beta9"));

            Assert.That(selection.Release.TagName, Is.EqualTo("V2.0.12-Beta1"));
            Assert.That(selection.PortableAsset, Is.Not.Null);
            Assert.That(selection.InstallerAsset, Is.Null);
        }

        [Test]
        public void AssetLessOlderReleasesDoNotFailAnUpToDateInstallation()
        {
            // This was the reported bug: the newest release has no assets, but it is older than the
            // installed build, so the result must be "up to date", not an error.
            var releases = new List<GitHubRelease>
            {
                CreateReleaseWithTag("V2.0.11-Beta9")
            };

            UpdateService.UpdateReleaseSelection selection =
                UpdateService.SelectUpdateRelease(releases, AppVersion.Parse("V2.0.12-Beta1"));

            Assert.That(selection.Release, Is.Null);
            Assert.That(selection.HasNewerReleaseWithoutPackage, Is.False);
        }

        [Test]
        public void ReportsMissingPackageOnlyWhenANewerReleaseExists()
        {
            var releases = new List<GitHubRelease>
            {
                CreateReleaseWithTag("V2.0.12-Beta2") // newer, but no package
            };

            UpdateService.UpdateReleaseSelection selection =
                UpdateService.SelectUpdateRelease(releases, AppVersion.Parse("V2.0.11-Beta9"));

            Assert.That(selection.Release, Is.Null);
            Assert.That(selection.HasNewerReleaseWithoutPackage, Is.True);
        }

        [Test]
        public void EqualVersionIsNotAnUpdate()
        {
            var releases = new List<GitHubRelease>
            {
                CreateReleaseWithTag("V2.0.12-Beta1", "EchoStem-Portable-2.0.12-Beta1.zip")
            };

            UpdateService.UpdateReleaseSelection selection =
                UpdateService.SelectUpdateRelease(releases, AppVersion.Parse("V2.0.12-Beta1"));

            Assert.That(selection.Release, Is.Null);
            Assert.That(selection.HasNewerReleaseWithoutPackage, Is.False);
        }

        private static GitHubRelease CreateReleaseWithTag(string tag, params string[] assetNames)
        {
            GitHubRelease release = CreateRelease(assetNames);
            release.TagName = tag;
            return release;
        }
        private static GitHubRelease CreateRelease(params string[] assetNames)
        {
            var release = new GitHubRelease
            {
                TagName = "V2.0.12-Beta1",
                Assets = new List<GitHubReleaseAsset>()
            };

            foreach (string assetName in assetNames)
            {
                release.Assets.Add(new GitHubReleaseAsset
                {
                    Name = assetName,
                    DownloadUrl = "https://example.invalid/" + assetName,
                    Size = 1
                });
            }

            return release;
        }
    }
}
