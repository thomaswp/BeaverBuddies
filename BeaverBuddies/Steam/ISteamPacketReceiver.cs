namespace BeaverBuddies.Steam
{
    public interface ISteamPacketReceiver
    {
        void RegisterSteamPacketListener(SteamPacketListener listener);
    }
}
