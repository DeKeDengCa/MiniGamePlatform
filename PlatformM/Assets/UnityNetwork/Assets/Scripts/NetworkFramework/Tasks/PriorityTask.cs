using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkFramework.Tasks
{
    public sealed class PriorityTask<T>
    {
        public string Name { get; }
        public TaskPriority Priority { get; }
        public CancellationTokenSource Cancellation { get; }
        public Func<CancellationToken, Task<T>> Work { get; }

        public PriorityTask(string name, TaskPriority priority, Func<CancellationToken, Task<T>> work)
        {
            Name = name;
            Priority = priority;
            Work = work ?? throw new ArgumentNullException(nameof(work));
            Cancellation = new CancellationTokenSource();
        }

        public void Cancel() => Cancellation.Cancel();
    }
}