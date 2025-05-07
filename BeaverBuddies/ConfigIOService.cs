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
    internal class ConfigIOService : IPostLoadableSingleton
    {
        public string ConfigFileName => "ReplayConfig";

        private ModRepository _modRepository;

        private string configPath = null;
        private JsonSerializerSettings deserializeSettings;

        public ConfigIOService(ModRepository modRepository)
        {
            _modRepository = modRepository;

            deserializeSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
            };
        }

        public void PostLoad()
        {
            if (!LoadConfigFromFile())
            {
                // If there's no config file, create a new one
                SaveConfigToFile();
            }
        }

        public bool LoadConfigFromFile()
        {
            string path = GetConfigPath();
            if (path == null) return false;
            // Create directories if they don't exit
            ReplayConfig config = new ReplayConfig();
            try
            {
                JsonConvert.PopulateObject(File.ReadAllText(configPath), config, deserializeSettings);
                EventIO.Config = config;
                Plugin.Log("Config loaded!");
                return true;
            }
            catch (Exception)
            {
                Plugin.LogWarning("Failed to load config file.");
                return false;
            }
        }

        public string GetConfigPath()
        {
            if (configPath != null) return configPath;

            var mod = _modRepository.Mods.Where(mod => mod.Manifest.Id == Plugin.ID && mod.Manifest.Version.Full == Plugin.Version && ModdedState.IsModded && mod.IsEnabled).FirstOrDefault();
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

        public void SaveConfigToFile()
        {
            string path = GetConfigPath();
            if (path == null) return;
            Plugin.Log($"Writing config to: {path}");
            try
            {
                File.WriteAllText(configPath, JsonConvert.SerializeObject(EventIO.Config, Formatting.Indented));
            }
            catch (Exception e)
            {
                Plugin.LogWarning($"Failed to save config: {e}");
            }
        }
    }
}
