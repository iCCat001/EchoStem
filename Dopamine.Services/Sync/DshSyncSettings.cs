using Digimezzo.Foundation.Core.Logging;
using Digimezzo.Foundation.Core.Settings;
using System;

namespace Dopamine.Services.Sync
{
    /// <summary>
    /// Safe access to the DSH sync settings.
    ///
    /// A settings namespace added after a user's Settings.xml was first created
    /// is not automatically present in that file: <see cref="SettingsClient.Get{T}"/>
    /// throws when the namespace or key is missing, and <see cref="SettingsClient.Set{T}"/>
    /// cannot create it. Reading through this helper avoids crashing startup on
    /// updated installations, while the bumped BaseSettings version lets the
    /// normal settings migration add the Integration defaults for existing users.
    /// </summary>
    public static class DshSyncSettings
    {
        public const string Section = "Integration";
        public const string EnabledKey = "DshSyncEnabled";
        public const string PortKey = "DshSyncPort";
        public const int DefaultPort = 38721;

        public static bool IsEnabled()
        {
            return Read(EnabledKey, false);
        }

        public static int GetPort()
        {
            int port = Read(PortKey, DefaultPort);
            return port > 0 && port <= 65535 ? port : DefaultPort;
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                SettingsClient.Set<bool>(Section, EnabledKey, enabled, true);
            }
            catch (Exception ex)
            {
                // If the settings file could not be migrated, the switch still
                // works for this session; log the persistence failure instead of
                // taking the settings page down.
                LogClient.Error("Could not save the DSH sync setting. Exception: {0}", ex.Message);
            }
        }

        private static T Read<T>(string key, T fallback)
        {
            try
            {
                return SettingsClient.Get<T>(Section, key);
            }
            catch (Exception)
            {
                try
                {
                    return SettingsClient.BaseGet<T>(Section, key);
                }
                catch (Exception)
                {
                    return fallback;
                }
            }
        }
    }
}
