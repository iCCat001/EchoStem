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
