using JetBrains.Annotations;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using static Utilities.Async.AwaiterExtensions;

namespace Utilities.Async
{
    /// <summary>
    /// Processes Coroutine and notifies completion.
    /// </summary>
    public class SimpleCoroutineAwaiter : ICriticalNotifyCompletion
    {
        private Exception exception;
        private Action continuation;

        public bool IsCompleted { get; private set; }

        [UsedImplicitly]
        public void GetResult()
        {
            if (!IsCompleted)
            {
                throw new InvalidOperationException("Tried to get result before task completed!");
            }

            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        public void Complete(Exception e = null)
        {
            if (IsCompleted)
            {
                throw new InvalidOperationException("Task has already been completed!");
            }

            IsCompleted = true;
            exception = e;

            // Always trigger the continuation on the unity thread
            // when awaiting on unity yield instructions.
            if (continuation != null)
            {
                RunOnUnityScheduler(continuation);
            }
        }

        public void OnCompleted(Action notifyContinuation)
            => UnsafeOnCompleted(notifyContinuation);

        public void UnsafeOnCompleted(Action notifyContinuation)
        {
            if (continuation != null)
            {
                throw new InvalidOperationException("task continuation is not null!");
            }

            if (IsCompleted)
            {
                throw new InvalidOperationException("Task has already been completed!");
            }

            continuation = notifyContinuation;
        }
    }

    /// <summary>
    /// Processes Coroutine and notifies completion with result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public class SimpleCoroutineAwaiter<T> : ICriticalNotifyCompletion
    {
        private Exception exception;
        private Action continuation;
        private T result;

        public bool IsCompleted { get; private set; }

        [UsedImplicitly]
        public T GetResult()
        {
            if (!IsCompleted)
            {
                throw new InvalidOperationException("Tried to get result before task completed!");
            }

            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return result;
        }

        public void Complete(T taskResult, Exception e = null)
        {
            if (IsCompleted)
            {
                throw new InvalidOperationException("Task has already been completed!");
            }

            IsCompleted = true;
            exception = e;
            result = taskResult;

            // Always trigger the continuation on the unity thread
            // when awaiting on unity yield instructions.
            if (continuation != null)
            {
                RunOnUnityScheduler(continuation);
            }
        }

        public void OnCompleted(Action notifyContinuation)
            => UnsafeOnCompleted(notifyContinuation);

        public void UnsafeOnCompleted(Action notifyContinuation)
        {
            if (continuation != null)
            {
                throw new InvalidOperationException("task continuation is not null!");
            }

            if (IsCompleted)
            {
                throw new InvalidOperationException("Task has already been completed!");
            }

            continuation = notifyContinuation;
        }
    }
}
