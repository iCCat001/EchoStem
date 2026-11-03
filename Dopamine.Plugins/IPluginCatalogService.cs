using System.Collections.Generic;

namespace Dopamine.Plugins
{
    /// <summary>
    /// Discovers the plugin packages bundled with the application and the ones
    /// installed by the user.
    /// </summary>
    public interface IPluginCatalogService
    {
        /// <summary>
        /// The application's bundled plugin root
        /// (<c>&lt;app&gt;\Plugins</c>).
        /// </summary>
        string BundledPluginsRoot { get; }

        /// <summary>
        /// The per-user plugin root
        /// (<c>%AppData%\Dopamine\Plugins</c>).
        /// </summary>
        string UserPluginsRoot { get; }

        /// <summary>
        /// Returns every valid package, bundled packages first, deduplicated by
        /// manifest id, sorted by display name.
        /// </summary>
        IReadOnlyList<PluginPackage> GetPlugins();
    }
}
