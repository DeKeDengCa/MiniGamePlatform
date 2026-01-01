using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Runtime;
using NetworkFramework.Utils;

namespace NetworkFramework.Tasks
{
    public class PriorityTaskScheduler
    {
        private readonly Dictionary<TaskPriority, Queue<IWrapper>> _queues = new()
        {
            { TaskPriority.Critical, new Queue<IWrapper>() },
            { TaskPriority.High, new Queue<IWrapper>() },
            { TaskPriority.Normal, new Queue<IWrapper>() },
            { TaskPriority.Low, new Queue<IWrapper>() },
        };

        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxConcurrent;
        private volatile bool _running;
        private readonly object _lock = new();

        public PriorityTaskScheduler(int maxConcurrent = 24)
        {
            _maxConcurrent = Math.Max(1, maxConcurrent);
            _semaphore = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
        }


        public void Enqueue<T>(PriorityTask<T> task,
            Action<T> onSuccess = null,
            Action<Exception> onError = null,
            Action onCanceled = null)
        {
            lock (_lock)
            {
                _queues[task.Priority].Enqueue(new Wrapper<T>(task, onSuccess, onError, onCanceled, this));
                if (!_running)
                {
                    _running = true;
                    _ = PumpLoop();
                }
            }
        }

        public void StopAll()
        {
            lock (_lock)
            {
                foreach (var q in _queues.Values)
                {
                    foreach (var item in q) item.Cancel();
                    q.Clear();
                }

                _running = false;
            }
        }

        private async Task PumpLoop()
        {
            while (true)
            {
                IWrapper next;
                lock (_lock)
                {
                    next = DequeueNext();
                    if (next == null)
                    {
                        _running = false;
                        return;
                    }
                }

                await _semaphore.WaitAsync().ConfigureAwait(false);
                _ = HandleOne(next);
            }
        }

        private IWrapper DequeueNext()
        {
            foreach (var p in new[] { TaskPriority.Critical, TaskPriority.High, TaskPriority.Normal, TaskPriority.Low })
            {
                var q = _queues[p];
                if (q.Count > 0) return q.Dequeue();
            }

            return null;
        }

        private async Task HandleOne(IWrapper wrapper)
        {
            try
            {
                await wrapper.Run().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                LoggerUtil.Log($"Task {wrapper} was cancelled");
                wrapper.NotifyCanceled();
            }
            catch (Exception ex)
            {
                LoggerUtil.LogException(ex);
                wrapper.NotifyError(ex);
            }
            finally
            {
                _semaphore.Release(); 
            }
        }

        // —— Wrapper 接口与实现 —— //
        private interface IWrapper
        {
            Task Run();
            void NotifyError(Exception ex);
            void NotifyCanceled();
            void Cancel();
        }

        private sealed class Wrapper<T> : IWrapper
        {
            private readonly PriorityTask<T> _task;
            private readonly Action<T> _onSuccess;
            private readonly Action<Exception> _onError;
            private readonly Action _onCanceled;
            private readonly PriorityTaskScheduler _scheduler;

            public Wrapper(PriorityTask<T> task, Action<T> onSuccess, Action<Exception> onError, Action onCanceled,
                PriorityTaskScheduler scheduler)
            {
                _task = task;
                _onSuccess = onSuccess;
                _onError = onError;
                _onCanceled = onCanceled;
                _scheduler = scheduler;
            }

            public async Task Run()
            {
                var result = await _task.Work(_task.Cancellation.Token).ConfigureAwait(false);
                LoggerUtil.Log("Wrapper.Run success, posting to main thread");
                UnityMainThread.Post(() => { _onSuccess?.Invoke(result); });
            }

            public void NotifyError(Exception ex) => UnityMainThread.Post(() => _onError?.Invoke(ex));
            public void NotifyCanceled() => UnityMainThread.Post(() => _onCanceled?.Invoke());
            public void Cancel() => _task.Cancel();
        }

    }
}