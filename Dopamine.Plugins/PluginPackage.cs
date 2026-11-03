namespace Dopamine.Plugins
{
    /// <summary>
    /// One plugin package discovered on disk: its parsed manifest plus the
    /// directory the package lives in.
    /// </summary>
    public sealed class PluginPackage
    {
        public PluginPackage(PluginManifest manifest, string directory, string manifestPath)
        {
            this.Manifest = manifest;
            this.Directory = directory;
            this.ManifestPath = manifestPath;
        }

        public PluginManifest Manifest { get; private set; }

        /// <summary>Absolute path of the package directory.</summary>
        public string Directory { get; private set; }

        /// <summary>Absolute path of this package's <c>plugin.json</c>.</summary>
        public string ManifestPath { get; private set; }
    }
}
