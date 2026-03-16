namespace TimberNet
{
    public interface ISocketListener
    {
        void Start();
        void Stop();
        ISocketStream AcceptClient();
    }
}
