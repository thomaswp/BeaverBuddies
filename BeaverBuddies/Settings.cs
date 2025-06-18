using BeaverBuddies.DesyncDetecter;
using Bindito.Core.Internal;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace BeaverBuddies
{
    // TODO: Localization translations!
    public class Settings : ModSettingsOwner
    {
        public ModSetting<string> ClientConnectionAddress { get; } =
            new("127.0.0.1", 
                ModSettingDescriptor.CreateLocalized(
                    "BeaverBuddies.Settings.ClientConnectionAddress"
                ).SetLocalizedTooltip("BeaverBuddies.Settings.ClientConnectionAddress.Tooltip")
            );

        public ModSetting<int> DefaultPort { get; } =
            new(25565, 
                ModSettingDescriptor.CreateLocalized(
                    "BeaverBuddies.Settings.Port"
                ).SetLocalizedTooltip("BeaverBuddies.Settings.Port.Tooltip")
            );

        public ModSetting<bool> ShowFirstTimerMessage { get; } =
            new(true,
                ModSettingDescriptor.CreateLocalized(
                    "BeaverBuddies.Settings.ShowFirstTimerMessage"
                )
        );

        public ModSetting<bool> ReportingConsent { get; } =
            new(false,
            ModSettingDescriptor.CreateLocalized(
                "BeaverBuddies.Settings.ReportingConsent"
            ).SetLocalizedTooltip("BeaverBuddies.ClientDesynced.ConsentMessage")
        );

        // ---- Steam Settings ----

        public ModSetting<bool> EnableSteamConnection { get; } =
            new(true,
            ModSettingDescriptor.CreateLocalized(
                "BeaverBuddies.Settings.EnableSteamConnection"
            ).SetLocalizedTooltip("BeaverBuddies.Settings.EnableSteamConnection.Tooltip")
        );

        public ModSetting<bool> FriendsCanJoinSteamGame { get; } =
            new(true,
            ModSettingDescriptor.CreateLocalized(
                "BeaverBuddies.Settings.FriendsCanJoinSteamGame"
            ).SetLocalizedTooltip("BeaverBuddies.Settings.FriendsCanJoinSteamGame.Tooltip")
        );

        // ---- Developer Settings ----

        public ModSetting<bool> AlwaysTrace { get; } =
            new(false,
            ModSettingDescriptor.CreateLocalized(
                "BeaverBuddies.Settings.AlwaysTrace"
            ).SetLocalizedTooltip("BeaverBuddies.Settings.AlwaysTrace.Tooltip")
        );

        public ModSetting<bool> SilenceLogging { get; } =
            new(false,
                ModSettingDescriptor.CreateLocalized(
                    "BeaverBuddies.Settings.SilenceLogging"
                ).SetLocalizedTooltip("BeaverBuddies.Settings.SilenceLogging.Tooltip")
        );

        // We keep a static instance because
        // 1) The settings are saved in a static manner, so all instances
        //    should be identical, and
        // 2) We need to access the settings frequently without an easy
        //    way to pass the instance around.
        private static Settings instance = null;

        /**
         * Indicates that the player has temporarily enabled debug mode.
         * Not serialized as a setting.
         */
        public static bool TemporarilyDebug { get; set; } = false;

        public static bool Debug => TemporarilyDebug || (instance?.AlwaysTrace.Value ?? false);

        public static bool VerboseLogging => !(instance?.SilenceLogging.Value == true);
        public static int Port => instance?.DefaultPort.Value ?? 25565;
        public static bool EnableSteam => instance?.EnableSteamConnection.Value ?? true;
        public static bool LobbyJoinable => instance?.FriendsCanJoinSteamGame.Value ?? true;

        public Settings(ISettings settings,
                        ModSettingsOwnerRegistry modSettingsOwnerRegistry,
                        ModRepository modRepository) : 
            base(settings, modSettingsOwnerRegistry, modRepository)
        {
            instance = this;
        }

        protected override string ModId => Plugin.ID;

    }
}
