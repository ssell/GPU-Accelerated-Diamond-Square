using System;
using System.Threading;
using UnityEngine;

namespace VertexFragment
{
    /// <summary>
    /// A worker in a <see cref="ThreadPool"/>.<para/>
    /// 
    /// Waits until a <see cref="JobBase"/> is assigned and then processes it.
    /// Once done, it returns to being idle awaiting the next job.
    /// </summary>
    public sealed class ThreadWorker
    {
        /// <summary>
        /// Lock used by <see cref="Monitor"/> to implement our spin-free waiting.
        /// </summary>
        public readonly object MonitorLock = new object();

        /// <summary>
        /// The current job being performed.
        /// </summary>
        public JobBase Job { get; private set; }

        /// <summary>
        /// Our cancellation token provided by the <see cref="ThreadPool"/>.
        /// </summary>
        private CancellationToken Cancellation;

        public ThreadWorker(CancellationToken cancellationToken)
        {
            Cancellation = cancellationToken;
        }

        /// <summary>
        /// Entry point for the worker which waits until a job is provided, performs the job, and then returns to waiting. <para/>
        /// 
        /// The waiting is implemented via <see cref="Monitor.Wait"/> and <see cref="Monitor.Pulse"/>, the pulse being performed by the <see cref="ThreadPool"/>.
        /// </summary>
        /// <param name="onWorkComplete"></param>
        public void Run(Action onWorkComplete)
        {
            while (!Cancellation.IsCancellationRequested)
            {
                lock (MonitorLock)
                {
                    while ((Job == null) && !Cancellation.IsCancellationRequested)
                    {
                        /**
                         * This will wait until another thread triggers Monitor.Pulse on our MonitorLock.
                         * The pulse is done by ThreadPool.AwakeWorkerWithJob. Once the pulse is received,
                         * the wait is ended and we return to the while loop.
                         * 
                         * If a job was assigned, or the cancellation token triggered, we will exit the
                         * loop and either process the job or end the thread.
                         * 
                         * This pattern is explained by:
                         * 
                         *     - https://stackoverflow.com/a/7448607/735425
                         *     - http://www.albahari.com/threading/part4.aspx#_Signaling_with_Wait_and_Pulse
                         */

                        Monitor.Wait(MonitorLock);
                    }
                }

                if (Job != null)
                {
                    try
                    {
                        Job.Execute(Cancellation);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    if (Job.OnJobComplete != null)
                    {
                        Job.OnJobComplete(Job);
                    }

                    Job = null;
                    onWorkComplete.Invoke();
                }
            }
        }

        /// <summary>
        /// Sets the job to be performed. Will return <c>false</c> if the worker is not idle.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public bool SetJob(JobBase job)
        {
            if (Job != null)
            {
                return false;
            }

            Job = job;

            return true;
        }
    }
}
