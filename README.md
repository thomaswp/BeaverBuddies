# BeaverBuddies

[![Last commit](https://img.shields.io/github/last-commit/thomaswp/BeaverBuddies?label=Last%20commit&color=lightgray)](https://github.com/thomaswp/BeaverBuddies/commits)
[![License](https://img.shields.io/github/license/thomaswp/BeaverBuddies?label=License&color=gray)](https://github.com/thomaswp/BeaverBuddies/blob/master/License.txt)
[![Timberborn 1.0](https://img.shields.io/badge/Timberborn_1.0-compatible-peru)](https://mechanistry.com)
[![Discord mod thread](https://img.shields.io/badge/Discord-mod_thread-mediumpurple)](https://discord.com/channels/558398674389172225/1203786573142032445)  
[![Steam Workshop](https://img.shields.io/badge/Steam_Workshop-available-royalblue)](https://steamcommunity.com/sharedfiles/filedetails/?id=3293380223)
[![mod.io](https://img.shields.io/badge/mod.io-available-limegreen)](https://mod.io/g/timberborn/m/beaverbuddies)

BeaverBuddies is a mod to allow multiplayer co-op in Timberborn.

> [!IMPORTANT]
> **If you would like to use the BeaverBuddies mod**, please see [the setup instructions in the wiki](https://github.com/thomaswp/BeaverBuddies/wiki)! This README is for developers.

## Contributing

We appreciate your help! To get started working on BeaverBuddies, see [the guide in the wiki](https://github.com/thomaswp/BeaverBuddies/wiki/Contributing).

## How to Build BeaverBuddies

1. Clone this repo `git clone git@github.com:thomaswp/BeaverBuddies`.
2. Set up DotNet C#.  
   For Windows, download & install [Visual Studio community edition](https://visualstudio.microsoft.com/vs/community).  
   For Mac, either run `brew install dotnet` or download & install [DotNet SDK](https://dotnet.microsoft.com/en-us/download).
3. Build the project.  
   For Visual Studio, open the solution & hit Ctrl+Shift+B.  
   For DotNet SDK, go to the BeaverBuddies directory & run `dotnet build`.  
   You may get a few "directory not found" errors. To fix these, open `BeaverBuddies/BeaverBuddies/env.props` and adjust the environmental variables there to point to your Timberborn installation & the necessary mods.

Building on Linux is similar to on Mac.

## How to Test Your Build

1. Make sure your project has been built with no errors.
2. Confirm that the mod files were copied to your Timberborn mods folder (e.g. `Documents/Timberborn/Mods/BeaverBuddies`.
3. Launch Timberborn and select the BeaverBuddies mod on the mod selection screen.  
   There may be multiple BeaverBuddies mod entries. The one with a "folder" icon next to it is your local build, select it.
