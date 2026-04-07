# Port Forwarding Guide

This guide walks you through setting up port forwarding for BeaverBuddies direct TCP connections. BeaverBuddies uses port **25565** by default (the same as Minecraft, so Minecraft port forwarding guides also apply).

You can also search for port forwarding guides specific to your router model.

## Step 1: Find Your Local IP Address

### Windows

1. Press the **Windows key**, type `cmd`, and press **Enter**
2. In the Command Prompt, type `ipconfig` and press **Enter**
3. Look for **IPv4 Address** under your active network connection
4. Write down that address (e.g., `192.168.1.105`) - this is your local IP

### macOS

1. Open **System Settings** > **Network**
2. Select your active connection (Wi-Fi or Ethernet)
3. Your IP address is displayed (e.g., `192.168.1.105`)

Alternatively, open Terminal and run:

```bash
ipconfig getifaddr en0
```

## Step 2: Access Your Router Settings

1. Open a web browser
2. Type your router's IP address in the address bar. Common addresses:
   - `192.168.0.1`
   - `192.168.1.1`
   - `10.0.0.1`
3. Press **Enter**
4. Log in with your router's username and password
   - Check the back/bottom of your router for default credentials
   - If you can't find them, search for your router model + "default login"

## Step 3: Find the Port Forwarding Section

Look for a section called **Port Forwarding**, **Virtual Server**, or **NAT**. It may be under:
- Advanced Settings
- Security
- Network Settings
- Firewall

> Every router interface is different. If you can't find it, search your router name + "port forwarding" online.

## Step 4: Create a Port Forwarding Rule

1. Click **Add**, **Create**, or **New**
2. Fill in the following:

| Field | Value |
|-------|-------|
| Name / Description | `Timberborn` (or any name you like) |
| Local IP / Internal IP | Your local IP from Step 1 |
| Internal Port | `25565` |
| External Port | `25565` |
| Protocol | **TCP** (if you can only choose one) or **TCP and UDP** |

3. If the form asks for a start and end port (or "translation" ports), enter `25565` for all of them
4. Save the rule

> If your router only lets you create one protocol at a time, create a rule for **TCP** first, then create a second rule for **UDP** with the same settings.

## Step 5: Verify

1. Start hosting a game in BeaverBuddies
2. Visit [yougetsignal.com/tools/open-ports](https://www.yougetsignal.com/tools/open-ports/)
3. Enter port `25565` and click **Check**
4. If it shows as **Open**, you're good to go

## Sharing Your IP Address

The host needs to share their **public** (external) IP address with the client. Find it at:
- [icanhazip.com](https://icanhazip.com/)
- [whatismyip.com](https://www.whatismyip.com/)

> Your public IP may change periodically. Check it each time you host.

## Alternatives to Port Forwarding

If you can't set up port forwarding:

- **Steam P2P**: Use Steam's peer-to-peer networking instead (see [User Guide](user-guide#option-1-steam-peer-to-peer-easiest))
- **Hamachi**: [vpn.net](https://vpn.net/) can simulate LAN play for free for small groups
- **Tailscale / ZeroTier**: Modern VPN alternatives that are often easier to set up than traditional port forwarding
