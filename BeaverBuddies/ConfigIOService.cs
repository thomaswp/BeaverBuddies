using BeaverBuddies.IO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Timberborn.Modding;
using Timberborn.SingletonSystem;

namespace BeaverBuddies
{
#pragma warning disable CS0618 // Type or member is obsolete
    internal class ConfigIOService : IPostLoadableSingleton
    {
        public string ConfigFileName => "ReplayConfig";

        private ModRepository _modRepository;
        private Settings _settings;

        private string configPath = null;
        private JsonSerializerSettings deserializeSettings;

        public ConfigIOService(
            ModRepository modRepository,
            Settings settings)
        {
            _modRepository = modRepository;
            _settings = settings;

            deserializeSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
            };
        }

        public void PostLoad()
        {
            if (LoadConfigFromFile(out ReplayConfig config))
            {
                _settings.ClientConnectionAddress.SetValue(config.ClientConnectionAddress);
                _settings.DefaultPort.SetValue(config.Port);
                _settings.SilenceLogging.SetValue(!config.Verbose);
                _settings.ShowFirstTimerMessage.SetValue(config.FirstTimer);
                _settings.ReportingConsent.SetValue(config.ReportingConsent);
                _settings.AlwaysTrace.SetValue(config.Verbose);
                _settings.AlwaysTrace.SetValue(config.AlwaysDebug);
                Plugin.Log("Transferred config from file to settings.");
                DeleteConfigFile();
            }
        }

        private bool LoadConfigFromFile(out ReplayConfig config)
        {
            string path = GetConfigPath();
            config = null;
            if (path == null) return false;
            // Create directories if they don't exit

            config = new ReplayConfig();
            try
            {
                JsonConvert.PopulateObject(File.ReadAllText(configPath), config, deserializeSettings);
                return true;
            }
            catch (Exception)
            {
                Plugin.LogWarning("Failed to load config file.");
                return false;
            }
        }

        private string GetConfigPath()
        {
            if (configPath != null) return configPath;

            var mod = _modRepository.Mods.Where(mod => mod.Manifest.Id == Plugin.ID).FirstOrDefault();
            if (mod == null)
            {
                Plugin.LogWarning("Cannot find mod from repository!");
                foreach (Mod otherMod in _modRepository.Mods)
                {
                    Plugin.Log($"{otherMod.Manifest.Id}: {otherMod.Manifest.Name}");
                }
                return null;
            }

            configPath = Path.Combine(mod.ModDirectory.Path, $"{ConfigFileName}.json");
            return configPath;
        }

        public void DeleteConfigFile()
        {
            string path = GetConfigPath();
            Plugin.Log($"Deleting config at path: {path}");
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"Failed to delete config: {e}");
            }
        }
    }
}
