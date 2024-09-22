using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TimberNet
{
    public interface ISocketStream
    {
        bool Connected { get; }

        int Read(byte[] buffer, int offset, int count);
        void Write(byte[] buffer, int offset, int count);
        void Close();
        Task ConnectAsync();
    }
}
