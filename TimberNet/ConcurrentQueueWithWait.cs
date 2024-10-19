using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TimberNet
{
    public class ConcurrentQueueWithWait<T>
    {
        private ConcurrentQueue<T> queue;
        ManualResetEvent hasAvailable = new ManualResetEvent(false);

        public ConcurrentQueueWithWait()
        {
            queue = new ConcurrentQueue<T>();
        }

        public void Enqueue(T item)
        {
            queue.Enqueue(item);
            hasAvailable.Set();
        }

        public bool WaitAndTryDequeue(out T item)
        {
            Wait();
            if (queue.TryDequeue(out item))
            {
                hasAvailable.Reset();
                if (!queue.IsEmpty)
                {
                    hasAvailable.Set();
                }
                return true;
            }
            return false;
        }

        public void Wait()
        {
            hasAvailable.WaitOne();
        }
        
    }
}
