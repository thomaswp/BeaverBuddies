using System;
using System.Collections.Generic;
using System.Text;

namespace TimberNet
{
    public interface ISocketListener
    {
        void Start();
        void Stop();
        ISocketStream AcceptClient();
    }
}
