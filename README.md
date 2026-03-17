# BeaverBuddies

BeaverBuddies is a mod to allow multiplayer co-op in Timberborn.

**If you would like to use the mod**, please see the documentation on the [wiki](https://github.com/thomaswp/BeaverBuddies/wiki)! This README is for develoeprs.

## Setup

1. Install BepInEx, currently [this version](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.22), by downloading the correct .zip folder, unzipping it, and putting the contents into your Timberborn game folder (likely something like `C:\Program Files (x86)\Steam\steamapps\common\Timberborn` on Windows). You should have a folder structure with something like `Timberborn\BepInEx\plugins\`.
2. Run Timberborn to finish installing BepInEx. If it was successful, you should have a `config` folder inside of your `BepInEx` folder.
  * Suggested: update `Timberborn\BepInEx\config\BepInEx.cfg` as follows to include console output:
```
[Logging.Console]

## Enables showing a console for log output.
# Setting type: Boolean
# Default value: false
Enabled = true # Change this to true
```
3. Install [Visual Studio 2022 community edition](https://visualstudio.microsoft.com/downloads/) and open this solution with it.
4. Build and run the project (Ctrl+shift+B). You may get errors about missing dependencies. To fix them, open `BeaverBuddies/BeaverBuddies/env.props` and adjust the environmental variables as needed.
   * `env.props` should have been created automatically, but if not you can copy `env.props.template` to `env.props`.
   * 

## Running the Mod

1. Make sure your project has been built with no errors. 
2. Confirm that the mod files were copied to your Timberborn mods folder (e.g. `Documets/Timberborn/Mods/BeaverBuddies`
3. Launch Timberborn and select the BeaverBuddies mod on the mod selection screen.

