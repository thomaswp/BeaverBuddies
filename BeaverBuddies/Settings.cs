using Bindito.Core;
using ModSettings.Core;
using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace BeaverBuddies
{
    internal class Settings : ModSettingsOwner
    {

        public ModSetting<int> ExampleSetting { get; } =
          new(0, ModSettingDescriptor.Create("Test setting!"));

        public Settings(ISettings settings,
                                       ModSettingsOwnerRegistry modSettingsOwnerRegistry,
                                       ModRepository modRepository) : base(
            settings, modSettingsOwnerRegistry, modRepository)
        {
        }

        protected override string ModId => Plugin.ID;

    }
}
