using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MSDAD
{
    namespace Server
    {
        class WorkQueue
        {
            public delegate void WorkDelegate();

            public ConcurrentQueue<AutoResetEvent> eventQueue = new ConcurrentQueue<AutoResetEvent>();

            private AutoResetEvent notEmpty = new AutoResetEvent(true);
            
            public void addWork(WorkDelegate work)
            {
                AutoResetEvent myEvent = new AutoResetEvent(false);
                eventQueue.Enqueue(myEvent);
                AutoResetEvent head;
                eventQueue.TryPeek(out head);
                while (head != myEvent)
                {
                    myEvent.WaitOne();
                    eventQueue.TryPeek(out head);
                }
                lock (this)
                {
                    work();
                    //Remove my event from queue
                    eventQueue.TryDequeue(out head);

                    eventQueue.TryPeek(out head);
                    head.Set();
                }
            }
        }

    }
}