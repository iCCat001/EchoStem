using Newtonsoft.Json;

namespace Dopamine.Plugins
{
    /// <summary>
    /// The <c>plugin.json</c> manifest shipped next to every EchoStem plugin
    /// package. It describes the package to EchoStem; the plugin's own runtime
    /// files (for example a DSH bundle's package.json and client.js) stay
    /// alongside it.
    /// </summary>
    public sealed class PluginManifest
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        /// <summary>
        /// Package kind, for example <c>dsh-web</c>. Kept as a free-form string so
        /// future integrations do not need a new catalog release.
        /// </summary>
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("entry")]
        public string Entry { get; set; }

        [JsonProperty("host")]
        public string Host { get; set; }
    }
}
