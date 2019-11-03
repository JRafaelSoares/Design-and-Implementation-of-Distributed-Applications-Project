using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MSDAD
{
    namespace Server
    {
        class WorkQueue
        {
            public delegate void WorkDelegate();
            public List<Thread> workQueue = new List<Thread>();
            private object AddLock = new object();

            public void addWork(WorkDelegate work)
            {
                lock (AddLock)
                {
                    workQueue.Add(Thread.CurrentThread);
                    while (workQueue.IndexOf(Thread.CurrentThread) != 0) Monitor.Wait(AddLock);
                    workQueue.Remove(Thread.CurrentThread);
                    work();
                    Monitor.Pulse(AddLock);
                }
            }
        }

    }
}