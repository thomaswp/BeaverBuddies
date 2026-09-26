using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using Timberborn.BlockObjectTools;
using Timberborn.Common;
using Timberborn.Debugging;
using Timberborn.QuickNotificationSystem;
using Timberborn.Reproduction;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.TerrainPhysics;

namespace BeaverBuddies.DevTools
{
    /**
     * Decides whether dev tools can be used in a co-op game. The host
     * controls it with a setting, which is sent to clients when they
     * join and whenever it changes.
     */
    public static class DevToolsPolicy
    {
        public const string DisabledByHostKey = "BeaverBuddies.DevTools.DisabledByHost";
        public const string NotSyncedKey = "BeaverBuddies.DevTools.NotSynced";

        // What the host allows, as last received by this client
        public static bool HostAllowsDevTools { get; set; } = true;

        public static bool IsHost => EventIO.Get() is ServerEventIO;

        public static bool IsAllowed => IsHost ? Settings.AllowDevTools : HostAllowsDevTools;

        /**
         * True when dev actions have to go through the co-op rules: a co-op
         * game is running and we're not replaying an event.
         */
        public static bool IsRestricted =>
            !ReplayService.IsReplayingEvents && ReplayEvent.GetReplayServiceIfReady() != null;

        /**
         * Returns true if the dev action may go ahead, otherwise tells the
         * player why it was blocked.
         */
        public static bool CheckAllowed()
        {
            if (!IsRestricted || IsAllowed) return true;
            Notify(DisabledByHostKey);
            return false;
        }

        /**
         * For dev actions that can't be synced: always blocked in co-op.
         */
        public static bool Block()
        {
            if (!IsRestricted) return true;
            Notify(IsAllowed ? NotSyncedKey : DisabledByHostKey);
            return false;
        }

        /**
         * Records a dev action as an event, like ReplayEvent.DoPrefix,
         * after checking it's allowed.
         */
        public static bool DoPrefix(Func<ReplayEvent> getEvent)
        {
            if (!CheckAllowed()) return false;
            return ReplayEvent.DoPrefix(getEvent);
        }

        /**
         * The host drops dev events from clients when dev tools are
         * disabled, so a client with an outdated policy can't change
         * the game.
         */
        public static bool ShouldDropReplayedEvent(ReplayEvent replayEvent)
        {
            return replayEvent is DevToolEvent && IsHost && !Settings.AllowDevTools;
        }

        private static void Notify(string key)
        {
            string message = RegisteredLocalizationService.T(key);
            Plugin.Log(message);
            SingletonManager.GetSingleton<DevToolsService>()?.Notify(message);
        }
    }

    public class DevToolsService : RegisteredSingleton, IPostLoadableSingleton
    {
        private readonly DevModeManager _devModeManager;
        private readonly QuickNotificationService _quickNotificationService;
        private readonly Settings _settings;

        // Services the dev events need when replaying. They aren't
        // singletons, so ReplayService can't look them up.
        public TemplateService TemplateService { get; }
        public IRandomNumberGenerator RandomNumberGenerator { get; }
        public DefaultBlockObjectPlacer DefaultBlockObjectPlacer { get; }
        public NewbornSpawner NewbornSpawner { get; }
        public TerrainDestroyer TerrainDestroyer { get; }

        public DevToolsService(DevModeManager devModeManager,
            QuickNotificationService quickNotificationService, Settings settings,
            TemplateService templateService, IRandomNumberGenerator randomNumberGenerator,
            DefaultBlockObjectPlacer defaultBlockObjectPlacer, NewbornSpawner newbornSpawner,
            TerrainDestroyer terrainDestroyer)
        {
            _devModeManager = devModeManager;
            _quickNotificationService = quickNotificationService;
            _settings = settings;
            TemplateService = templateService;
            RandomNumberGenerator = randomNumberGenerator;
            DefaultBlockObjectPlacer = defaultBlockObjectPlacer;
            NewbornSpawner = newbornSpawner;
            TerrainDestroyer = terrainDestroyer;
        }

        public void PostLoad()
        {
            _settings.AllowDevToolsInCoop.ValueChanged += OnAllowDevToolsChanged;
            EnforcePolicy();
        }

        public void Notify(string message)
        {
            _quickNotificationService.SendWarningNotification(message);
        }

        public void EnforcePolicy()
        {
            if (!DevToolsPolicy.IsAllowed && _devModeManager.Enabled)
            {
                _devModeManager.Disable();
            }
        }

        private void OnAllowDevToolsChanged(object sender, bool allowed)
        {
            // Only the host's setting matters
            if (!DevToolsPolicy.IsHost) return;
            EnforcePolicy();
            ReplayEvent.DoPrefix(() => new DevToolsAllowedEvent() { allowed = allowed });
        }
    }

    /**
     * Sent by the host when it changes whether dev tools are allowed.
     */
    [Serializable]
    public class DevToolsAllowedEvent : ReplayEvent
    {
        public bool allowed;

        public override void Replay(IReplayContext context)
        {
            DevToolsPolicy.HostAllowsDevTools = allowed;
            context.GetSingleton<DevToolsService>()?.EnforcePolicy();
        }

        public override string ToActionString()
        {
            return $"Dev tools allowed by host: {allowed}";
        }
    }

    /**
     * Base class for events caused by dev tools, so the host can drop
     * them when dev tools are disabled.
     */
    public abstract class DevToolEvent : ReplayEvent
    {
    }
}
