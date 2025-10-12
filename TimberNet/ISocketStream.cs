using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace TimberNet
{
    public interface ISocketStream
    {
        bool Connected { get; }

        string? Name { get; }

        int MaxChunkSize { get; }
        int MaxBytesPerSecond { get; }

        int Read(byte[] buffer, int offset, int count);
        void Write(byte[] buffer, int offset, int count);
        void Close();
        Task ConnectAsync();

        public byte[] ReadUntilComplete(int count)
        {
            byte[] bytes = new byte[count];
            ReadUntilComplete(bytes, count);
            return bytes;
        }


        public void ReadUntilComplete(byte[] buffer, int count)
        {
            // TODO: How should this fail?
            int totalBytesRead = 0;
            while (totalBytesRead < count)
            {
                int bytesRead = Read(buffer, totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    // No more data, the connection may be closed or interrupted
                    // TODO: Not exactly sure what the right error handling is here
                    throw new IOException("Unexpected end of stream.");
                }
                totalBytesRead += bytesRead;
            }
        }
    }
}
