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
3. Install TimberAPI, using the same version in [mod.json](BeaverBuddies/mod.json), by downloading the "modio" file, and putting its contents into your `Timberborn\BepInEx\plugins\` folder, such that it now contains a folder called `TimberAPI`.
4. Install [Visual Studio 2022 community edition](https://visualstudio.microsoft.com/downloads/)) and open this solution with it.
5. Go to Tools->Options->Nuget Package Manager->Package Sources and click the green [+] button. Call it BepInEx and give it the URL `https://nuget.bepinex.dev/v3/index.json`.
6. Create a folder in `Timberborn\BepInEx\plugins\` called `BeaverBuddies`. Make note of the location of this folder.
7. Open the BeaverBuddies project (by double-clicking on it in Visual Studio). Find the Build->Events->Post build event option and make sure that the path it gives matches your path to Timberborn. By default it reads:
```
xcopy /y "$(ProjectDir)$(OutDir)*"  "C:\Program Files (x86)\Steam\steamapps\common\Timberborn\BepInEx\plugins\BeaverBuddies"
```
8. Build and run the project (Ctrl+shift+B). You should see some .dll files in the `BeaverBuddies` folder you created earlier.

## Running the Mod

1. Make sure your project has been built with no errors. 
2. Follow the instructions [on the Wiki](https://github.com/thomaswp/BeaverBuddies/wiki/Installation-and-Running#running-a-game).

