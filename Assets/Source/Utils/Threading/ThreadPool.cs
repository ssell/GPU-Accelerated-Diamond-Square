using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace VertexFragment
{
    /// <summary>
    /// A self-processing thread pool / queue.
    /// </summary>
    public sealed class ThreadPool
    {
        /// <summary>
        /// The number of pooled threads.
        /// </summary>
        public int ThreadCount { get; private set; }

        /// <summary>
        /// The maximum amount of time, by default, that <see cref="Sync"/> will wait for.
        /// </summary>
        public int MaxSyncWaitMs { get; set; } = 10000;

        /// <summary>
        /// The number of workers currently processing a job.
        /// </summary>
        public int ActiveWorkers { get { return ActiveWorkerCount; } }

        /// <summary>
        /// Collection of all of the <see cref="ThreadWorker"/>s in the pool.
        /// </summary>
        private readonly List<ThreadWorker> Workers;

        /// <summary>
        /// Cancellation token source for the entire pool.
        /// </summary>
        private readonly CancellationTokenSource PoolCancellationTokenSource;

        /// <summary>
        /// The number of workers currently processing a job.
        /// </summary>
        private int ActiveWorkerCount = 0;

        /// <summary>
        /// A queue of jobs to be processed.
        /// </summary>
        private Queue<JobBase> JobQueue = new Queue<JobBase>();

        private object JobQueueLock = new object();

        public ThreadPool(int threadCount)
        {
            ThreadCount = threadCount;
            PoolCancellationTokenSource = new CancellationTokenSource();
            Workers = new List<ThreadWorker>(ThreadCount);
        }

        ~ThreadPool()
        {
            PoolCancellationTokenSource.Cancel();

            foreach (var worker in Workers)
            {
                AwakeWorkerWithJob(worker, null);
            }
        }

        /// <summary>
        /// Builds all threads to be stored in the pool.
        /// Does not throw, and will return <c>false</c> on failure.
        /// </summary>
        /// <returns></returns>
        public bool TryBuild()
        {
            try
            {
                Build();
                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return false;
            }
        }

        /// <summary>
        /// Builds all threads to be stored in the pool. May throw.
        /// </summary>
        public void Build()
        {
            for (int i = 0; i < ThreadCount; ++i)
            {
                ThreadWorker worker = new ThreadWorker(PoolCancellationTokenSource.Token);

                Thread thread = new Thread(() => { worker.Run(OnWorkDone); })
                {
                    IsBackground = true
                };

                thread.Start();

                Workers.Add(worker);
            }
        }

        /// <summary>
        /// Runs the provided job on the first idle worker.
        /// If no workers are available then returns <c>false</c>.
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public bool Run(JobBase job)
        {
            int workerIndex = GetNextIdleWorker();

            if (workerIndex == -1)
            {
                // No available workers.
                return false;
            }

            AwakeWorkerWithJob(Workers[workerIndex], job);

            return true;
        }

        /// <summary>
        /// Adds a job to the internal <see cref="JobQueue"/>. The job will be run when there is an available worker.<para/>
        /// 
        /// Note that the thread pool does not directly provide a way to alert on job completion, you can instead define and
        /// provide a callback on the job implementation itself.
        /// </summary>
        /// <param name="job"></param>
        public void EnqueueJob(JobBase job)
        {
            lock (JobQueueLock)
            {
                JobQueue.Enqueue(job);
            }

            RunFrontJob();
        }

        /// <summary>
        /// Attempts to run the job at the front of the queue.
        /// Returns true if there was a job at the front and it was successfully added to a background worker.
        /// </summary>
        private bool RunFrontJob()
        {
            lock (JobQueueLock)
            {
                if ((JobQueue.Count > 0) && Run(JobQueue.Peek()))
                {
                    JobQueue.Dequeue();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the number of jobs awaiting processing in the job queue.
        /// </summary>
        /// <returns></returns>
        public int JobQueueLength()
        {
            return JobQueue.Count;
        }

        /// <summary>
        /// Spins up to <see cref="MaxSyncWaitMs"/> milliseconds until no active workers remain.
        /// Will return <c>false</c> if the sync timed out.
        /// </summary>
        public bool Sync()
        {
            return Sync(MaxSyncWaitMs);
        }

        /// <summary>
        /// Spins until no active workers remain.
        /// Will return <c>false</c> if the sync timed out.
        /// </summary>
        /// <param name="maxWaitMs"></param>
        public bool Sync(int maxWaitMs)
        {
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                SpinWait.SpinUntil(() =>
                {
                    return (IsEmpty() || (sw.ElapsedMilliseconds > 100));
                });

                if (IsEmpty())
                {
                    return true;
                }

                RunFrontJob();
            }

            return false;
        }

        /// <summary>
        /// Returns true if there are currently no jobs in the queue or being processed.
        /// </summary>
        /// <returns></returns>
        public bool IsEmpty()
        {
            return (JobQueueLength() == 0) && (ActiveWorkers == 0);
        }

        /// <summary>
        /// Awakes the given idle worker and assigns it a job to perform.
        /// </summary>
        /// <param name="worker"></param>
        /// <param name="job"></param>
        private void AwakeWorkerWithJob(ThreadWorker worker, JobBase job)
        {
            lock (worker.MonitorLock)
            {
                if ((worker != null) && worker.SetJob(job))
                {
                    Interlocked.Increment(ref ActiveWorkerCount);
                }

                Monitor.Pulse(worker.MonitorLock);
            }
        }

        /// <summary>
        /// Retrieves the index into <see cref="Workers"/> of the first idle worker. 
        /// If none are found, returns <c>-1</c>.
        /// </summary>
        /// <returns></returns>
        private int GetNextIdleWorker()
        {
            for (int i = 0; i < Workers.Count; ++i)
            {
                if (Workers[i].Job == null)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Invoked by a <see cref="ThreadWorker"/> when it is done with its job.
        /// </summary>
        private void OnWorkDone()
        {
            Interlocked.Decrement(ref ActiveWorkerCount);

            // Now that this worker is free, run an idle job on it.
            RunFrontJob();
        }

        /// <summary>
        /// Cancels all running jobs.
        /// </summary>
        public void Cancel()
        {
            PoolCancellationTokenSource.Cancel();
        }
    }
}
