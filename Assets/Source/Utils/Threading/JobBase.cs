using System;
using System.Threading;

namespace VertexFragment
{
    public abstract class JobBase
    {
        public Action<JobBase> OnJobComplete { get; set; }

        public abstract void Execute(CancellationToken cancellationToken);
    }
}
