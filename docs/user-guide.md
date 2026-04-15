# User Guide

This page covers installing, configuring, and playing BeaverBuddies as a player. For developer documentation, see the [Getting Started](getting-started) guide.

## Features

- Players work together to build a beaver civilization in real-time
- Each player has their own camera and user interface
- All resources and abilities are shared, similar to co-op in Stardew Valley, Factorio, or Parkitect
- Players who want more independence can create their own [Districts](https://timberborn.fandom.com/wiki/Districts) to control separate areas while still trading resources
- Host and connect from in-game menus
- Supports both direct TCP connections and Steam peer-to-peer networking

> **Note**: BeaverBuddies is under active development. You may experience desyncs or crashes. The mod may not work on the most complex saves yet. Help improve the mod by [reporting bugs](https://github.com/thomaswp/BeaverBuddies/issues).

## Installing BeaverBuddies

### Steam (Recommended)

1. Go to the [BeaverBuddies Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3293380223)
2. Click **Subscribe**
3. Launch Timberborn - the mod should appear in your Mods list:

![Mod selection screen](https://github.com/user-attachments/assets/82489f01-3624-45ec-923a-cd214d843295)

4. Disable other mods, as they are likely incompatible with BeaverBuddies

### Non-Steam

The recommended method is using the [Mod Manager](https://mod.io/g/timberborn/m/mod-manager):

1. Install the Mod Manager mod (see [this guide from Mod.io](https://mod.io/g/timberborn/r/how-to-install-mods))
2. Open Timberborn and click the "Mod manager" button in the main menu
3. Search for "Beaver Buddies" and click **Download**

![Mod Manager search](https://github.com/thomaswp/BeaverBuddies/assets/1750176/9dcfbdb6-7465-43e9-9b54-a31e07db1a15)

4. Disable other mods
5. Restart Timberborn

Without the Mod Manager:

1. Download the latest release from [Mod.io](https://mod.io/g/timberborn/m/beaverbuddies)
2. Install using the [Mod.io installation guide](https://mod.io/g/timberborn/r/how-to-install-mods)

## Network Setup

BeaverBuddies supports two networking modes. Crossplay between the two modes is supported for 3+ player sessions.

### Option 1: Steam Peer-to-Peer (Easiest)

When the host starts a game, they can invite players via Steam. This uses [Steam's peer-to-peer networking](https://help.steampowered.com/en/faqs/view/1433-AD20-F11D-B71E) and requires no port forwarding. Both players must have the Steam version of Timberborn.

- Pros: No network configuration needed
- Cons: Slightly slower than direct TCP, requires Steam, still in beta

### Option 2: Direct TCP (Port Forwarding)

The host forwards port **25565** (configurable in Settings) to their computer. See the [Port Forwarding Guide](port-forwarding) for detailed instructions.

- Pros: Faster, more reliable, works without Steam
- Cons: Requires port forwarding setup

If you don't want to port forward, you can use tools like [Hamachi](https://vpn.net/) to simulate LAN play.

## Running a Game

### As the Host

1. Find your public IP address (e.g., from [icanhazip.com](https://icanhazip.com/)) and share it with the client
2. If starting fresh, create a new game, save it, and exit to the main menu
3. From the main menu, select **Load Game** and find your save
4. Click the **Host co-op game** button (instead of "Load"):

![Host co-op game button](https://github.com/thomaswp/BeaverBuddies/assets/1750176/f999cf5b-7362-4c2c-b06c-de2efa7e092f)

5. Invite the client:
   - **Steam**: Use the Steam invite dialog, or they can join via the Steam chat menu
   - **Direct connect**: Share your IP address for them to enter
6. Wait for the client to appear in your list of joined players:

![Waiting for players dialog](https://github.com/user-attachments/assets/e12eda1a-091d-484b-b449-25508fdad605)

7. Click **Start Game**
8. Unpause and play!

### As the Client

1. Launch Timberborn and wait for the host to start hosting
2. Connect to the host:
   - **Direct connect**: Click **Join co-op game**, enter the host's IP address, and click OK
   - **Steam**: Accept the host's invitation or right-click their name in Steam chat and join their game
3. You'll automatically receive the save file and start loading
4. Once loaded, let the host know you're ready
5. Unpause and play!

## Troubleshooting

### The client can't connect to the server

- Verify the host's **current** IP address (it may have changed since last session)
- If using direct connect, verify port forwarding is working. The host can check at [yougetsignal.com](https://www.yougetsignal.com/tools/open-ports/) by entering their port (default: 25565)
- If using a non-default port, make sure both host and client have the correct port configured in Settings
- If Steam P2P isn't working, try direct TCP as a fallback

### The client desynced

1. Have the host **save the game** immediately
2. You should be able to reload from that save
3. Consider [filing a bug report](https://github.com/thomaswp/BeaverBuddies/issues) using the "Bug Report" template

Tips to reduce desyncs:
- Make sure the host has loaded a fresh save and has **not unpaused** before the client connects
- If loading an older save with lots of progress, try starting a new game instead to isolate the issue
- Make sure no other mods are running alongside BeaverBuddies
- If you can identify what action caused the desync, include that in your bug report

### The game crashed

File a [bug report](https://github.com/thomaswp/BeaverBuddies/issues). **Don't restart** either player's game before gathering logs, as that will delete the log files that help diagnose the issue.

## FAQ

### How many players does the game support?

Theoretically any number, but it's only been tested with 2. Support for more players is planned.

### Does the mod work cross-platform (Windows and Mac)?

Some users have reported success. If you run into problems, [let us know](https://github.com/thomaswp/BeaverBuddies/issues).

### Will this mod break my save file?

We **strongly** recommend backing up saves before using any mod. BeaverBuddies does not directly change your save files, so there should be no permanent damage. Crashes may lose temporary work, but autosave functions normally. We recommend starting with a new save for multiplayer.

### What other mods work with BeaverBuddies?

Currently, using other mods with BeaverBuddies is discouraged as they will likely cause desyncs. Simple mods that don't involve randomness or UI actions *might* work if both host and client install them, but this is not officially supported.

### Do both players need to own Timberborn?

Yes. Both players need their own copy.

### How do I stop using co-op mode?

- If installed via Steam Workshop, simply unsubscribe or disable it in the mod list
- If installed via Mod Manager, disable it there
- Otherwise, remove the BeaverBuddies folder from your mods folder

### How can I suggest a feature?

Create a [new issue](https://github.com/thomaswp/BeaverBuddies/issues) using the "Feature request" template. The goal of BeaverBuddies is intentionally limited to supporting the main game in co-op mode.
