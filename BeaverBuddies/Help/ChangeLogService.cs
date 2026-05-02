using System.Linq;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace BeaverBuddies.Help
{
    public class ChangeLogService : IPostLoadableSingleton
    {
        private const string VersionKey = "BeaverBuddies.LastSeenVersion";

        private DialogBoxShower _dialogBoxShower;

        internal ChangeLogService(
            DialogBoxShower dialogBoxShower,
            Settings settings
        )
        {
            _dialogBoxShower = dialogBoxShower;
        }

        private void UpdateLastSeenVersion()
        {
            Plugin.Log("Updating last seen version to " + Plugin.Version);
            PlayerPrefs.SetString(VersionKey, Plugin.Version);
        }

        private bool ShouldShowChangeLog()
        {
            string lastSeenVersion = PlayerPrefs.GetString(VersionKey, null);
            Plugin.Log("Last seen version: " + lastSeenVersion);
            return lastSeenVersion != Plugin.Version;
        }

        private string GetLastThreeChanges()
        {
            // Intentional choice not to localize the changelog (sorry!).
            // Too high a barrier, and not important enough.
            string changelog = Resources.ChangeLog.Replace("\r\n", "\n");
            string[] lines = changelog.Split("\n\n");
            string prefix = "BeaverBuddies Changelog:\n\n";
            if (lines.Length <= 3)
            {
                return prefix + changelog;
            }
            else
            {
                return prefix + string.Join("\n\n", lines.Take(3));
            }
        }

        public void PostLoad()
        {
            Plugin.Log("Post Load");
            // Don't show the changelog if they're already seeing the first timer message
            if (Settings.ShouldShowFirstTimerMessage || !ShouldShowChangeLog())
            {
                return;
            }

            // TODO: Ideally, we'd have a setting for whether to show this
            // (always, if new, never), but I think 99% of people will just want "if new"

            _dialogBoxShower.Create()
                .SetMessage(GetLastThreeChanges())
                .SetConfirmButton(() => UpdateLastSeenVersion())
                .Show();
        }
    }
}
