﻿using BeaverBuddies.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Timberborn.Options;
using Timberborn.OptionsGame;
using Timberborn.TimeSystem;
using Timberborn.UILayoutSystem;
using static BeaverBuddies.SingletonManager;

namespace BeaverBuddies.Events
{
    [Serializable]
    public class SpeedSetEvent : ReplayEvent
    {
        public float speed;

        public override void Replay(IReplayContext context)
        {
            SpeedManager sm = context.GetSingleton<SpeedManager>();
            Plugin.Log($"Event: Changing speed from {sm.CurrentSpeed} to {speed}");
            if (sm.CurrentSpeed != speed) sm.ChangeSpeed(speed);

            ReplayService replayService = context.GetSingleton<ReplayService>();
            if (speed != replayService.TargetSpeed)
            {
                Plugin.Log($"Event: Changing target speed from {replayService.TargetSpeed} to {speed}");
                replayService.SetTargetSpeed(speed);
            }
        }
    }

    [ManualMethodOverwrite]
    /*
        04/19/2025
		if (!_isLocked)
		{
			Time.timeScale = ScaleSpeed(speed);
			CurrentSpeed = speed;
			_eventBus.Post(new CurrentSpeedChangedEvent(CurrentSpeed));
		}
     */
    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.ChangeSpeed))]
    public class SpeedChangePatcher
    {
        private static bool silently = false;

        public static void SetSpeedSilently(SpeedManager speedManager, float speed)
        {
            silently = true;
            speedManager.ChangeSpeed(speed);
            silently = false;
        }

        static bool Prefix(SpeedManager __instance, ref float speed)
        {
            if (!ReplayService.IsLoaded) return true;
            // No need to log speed changes to current speed
            if (__instance.CurrentSpeed == speed) return true;
            // Also don't log if we're silent
            if (silently) return true;

            var replayService = ReplayEvent.GetReplayServiceIfReady();
            if (replayService == null) return true;

            replayService.RecordEvent(new SpeedSetEvent()
            {
                speed = speed
            });

            if (EventIO.ShouldPlayPatchedEvents)
            {
                // If this will actually change the speed, make sure
                // we shouldn't pause instead.
                if (EventIO.ShouldPauseTicking) speed = 0;
                return true;
            }
            return false;
        }
    }

    // This is now configurable via Settings.ReducePausesEnable. If we don't freeze, it could
    // definitely cause some possible invalid operations (e.g. deleting a building
    // that's not there anymore), but in theory these errors get caught before
    // sending to the server. In practice, though, there could be side-effects of
    // and aborted event. For clients, I think this is always a possibility, regardless
    // of whether we freeze, since it's always happening at a delay.
    [ManualMethodOverwrite]
    /*
        04/19/2025
    	if (!_isLocked)
		{
			_speedBefore = CurrentSpeed;
			ChangeSpeed(value);
			_isLocked = true;
			_eventBus.Post(new SpeedLockChangedEvent(_isLocked));
		}
     */
    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.ChangeAndLockSpeed))]
    public class SpeedLockPatcher
    {
        // When true, skip the next ChangeAndLockSpeed call (used by various UI events when ReducePauses is enabled)
        public static bool SkipNextSpeedLock = false;
        static bool Prefix(SpeedManager __instance, float value)
        {
            if (SkipNextSpeedLock)
            {
                SkipNextSpeedLock = false;
                return false; // skip original; do not modify speed or lock
            }
            // Clients should never freeze for dialogs. Main menu will be
            // handled separately.
            if (EventIO.Get()?.UserEventBehavior == UserEventBehavior.Send)
            {
                return false;
            }

            if (!__instance._isLocked)
            {
                __instance._speedBefore = __instance.CurrentSpeed;
                SpeedChangePatcher.SetSpeedSilently(__instance, value);
                __instance._isLocked = true;
                __instance._eventBus.Post(new SpeedLockChangedEvent(__instance._isLocked));
            }
            return false;
        }
    }

    [ManualMethodOverwrite]
    /*
     	04/19/2025
        if (_isLocked)
		{
			_isLocked = false;
			ChangeSpeed(_speedBefore);
			_eventBus.Post(new SpeedLockChangedEvent(_isLocked));
		}
     */
    [HarmonyPatch(typeof(SpeedManager), nameof(SpeedManager.UnlockSpeed))]
    public class SpeedUnlockPatcher
    {
        static bool Prefix(SpeedManager __instance)
        {
            // Clients should never unfreeze for dialogs. See above.
            if (EventIO.Get()?.UserEventBehavior == UserEventBehavior.Send)
            {
                return false;
            }

            if (__instance._isLocked)
            {
                __instance._isLocked = false;
                SpeedChangePatcher.SetSpeedSilently(__instance, __instance._speedBefore);
                __instance._eventBus.Post(new SpeedLockChangedEvent(__instance._isLocked));
            }
            return false;
        }
    }

    [Serializable]
    class ShowOptionsMenuEvent : SpeedSetEvent
    {
        public ShowOptionsMenuEvent()
        {
            speed = 0;
        }

        public override void Replay(IReplayContext context)
        {
            base.Replay(context);
            context.GetSingleton<IOptionsBox>().Show();
        }
    }

    // We make showing the options menu a synced game event, rather than
    // a non-synced UI action, for two reasons:
    // 1) This ensures that the Options menu is always shown when a full
    //    tick has been completed.
    // 2) This will give other plays a visual clue about why the game has
    //    paused.
    // However, only the host will be able to unpause, and only by manually
    // setting the game speed, since they won't process any events by clients
    // while they have a panel (including this one) up (I think...).
    [HarmonyPatch(typeof(GameOptionsBox), nameof(GameOptionsBox.Show))]
    public class GameOptionsBoxShowPatcher
    {
        static bool Prefix()
        {
            // If the user wants to reduce pauses, don't create an event or pause the game.
            // Let each player open their own menu independently.
            if (Settings.ReducePausesEnabled)
            {
                // Signal that the next lock attempt should be skipped
                SpeedLockPatcher.SkipNextSpeedLock = true;
                return true;
            }

            // Otherwise, treat showing options as a synced event that pauses.
            return ReplayEvent.DoPrefix(() => new ShowOptionsMenuEvent());
        }
    }

    // OverlayPanelSpeedLocker is triggering ChangeAndLockSpeed via OnPanelShown
    // We suppress its locking behavior entirely when ReducePauses is enabled to avoid unintended pauses
    // for overlay/submenus.
    [HarmonyPatch(typeof(OverlayPanelSpeedLocker), "OnPanelShown")]
    public class OverlayPanelSpeedLockerShowPatcher
    {
        static bool Prefix()
        {
            if (Settings.ReducePausesEnabled)
            {
                // Ensure that even if original would call ChangeAndLockSpeed, it gets skipped.
                SpeedLockPatcher.SkipNextSpeedLock = true;
                return false;
            }
            return true;
        }
    }
}
