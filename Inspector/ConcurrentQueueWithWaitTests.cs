using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimberNet;

namespace Inspector
{
    public class ConcurrentQueueWithWaitTests
    {
        public static void Test() {
            var queue = new ConcurrentQueueWithWait<int>();

            new Task(() =>
            {
                for (int i  = 0; i < 10; i++) 
                {
                    queue.Enqueue(1);
                    Thread.Sleep(130);
                }
            }).Start();

            new Task(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    queue.Enqueue(2);
                    Thread.Sleep(170);
                }
            }).Start();

            for (int i = 0; i < 20; i++)
            {
                queue.WaitAndTryDequeue(out int item);
                Console.WriteLine(item);
            }
        }
    }
}
