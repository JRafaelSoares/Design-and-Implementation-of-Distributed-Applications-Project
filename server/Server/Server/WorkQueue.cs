using System.Collections.Concurrent;
using System.Threading;

namespace MSDAD
{
    namespace Server
    {
        class WorkQueue
        {
            public delegate void WorkDelegate();

            public ConcurrentQueue<AutoResetEvent> eventQueue = new ConcurrentQueue<AutoResetEvent>();

            public void AddWork(WorkDelegate work)
            {
                AutoResetEvent myEvent = new AutoResetEvent(false);
                eventQueue.Enqueue(myEvent);
                eventQueue.TryPeek(out AutoResetEvent head);
                while (head != myEvent)
                {
                    head.Dispose();
                    myEvent.WaitOne();
                    eventQueue.TryPeek(out head);
                }
                head.Dispose();
                work();

                //lock to prevent race condition
                lock (this)
                {
                    //Remove my event from queue
                    eventQueue.TryDequeue(out head);
                    head.Dispose();
                    //signal next thread
                    eventQueue.TryPeek(out head);
                    head.Set();
                    head.Dispose();
                    
                }
            }
        }

    }
}