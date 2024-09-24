using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimberNet
{
    public class MultiSocketListener : ISocketListener
    {
        private readonly List<ISocketListener> listeners = new List<ISocketListener>();
        private readonly ConcurrentQueueWithWait<ISocketStream> accepted = new ConcurrentQueueWithWait<ISocketStream>();
        private bool isAccepting = false;
        private bool isStopped = false;

        public MultiSocketListener(params ISocketListener[] listeners) 
        {
            this.listeners.AddRange(listeners);
        }

        public ISocketStream AcceptClient()
        {
            if (!isAccepting)
            {
                StartAccpting();
                isAccepting = true;
            }
            accepted.WaitAndTryDequeue(out ISocketStream socket);
            return socket;
        }

        private void StartAccpting()
        {
            foreach (var listener in listeners)
            {
                Task.Run(() =>
                {
                    while (!isStopped)
                    {
                        accepted.Enqueue(AcceptClient());
                    }
                });
            }
        }

        public void Start()
        {
            listeners.ForEach(listener => listener.Start());
        }

        public void Stop()
        {
            listeners.ForEach(listener => listener.Stop());
            isStopped = true;
        }
    }
}
