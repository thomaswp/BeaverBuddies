using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TimberNet
{

    public class TCPListenerWrapper : ISocketListener
    {
        private readonly TcpListener listener;

        public TCPListenerWrapper(int port)
        {
            // Listen on IPv6 and IPv4
            listener = new TcpListener(IPAddress.IPv6Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        }

        public ISocketStream AcceptClient()
        {
            return new TCPClientWrapper(listener.AcceptTcpClient());
        }

        public void Start()
        {
            listener.Start();
        }

        public void Stop()
        {
            listener.Stop();
        }
    }
}
