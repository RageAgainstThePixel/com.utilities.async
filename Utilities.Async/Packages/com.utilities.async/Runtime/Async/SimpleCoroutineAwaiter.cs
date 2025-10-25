// Licensed under the MIT License. See LICENSE in the project root for license information.

using JetBrains.Annotations;
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using Utilities.Async.Internal;

namespace Utilities.Async
{
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
                AwaiterExtensions.RunOnUnityScheduler(continuation);
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

    public readonly struct CoroutineAwaiter : ICriticalNotifyCompletion
    {
        private static readonly SendOrPostCallback postCallback = state => ((Action)state)();
        private readonly IEnumerator coroutine;

        public CoroutineAwaiter(IEnumerator coroutine)
            => this.coroutine = coroutine;

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
        {
            if (SyncContextUtility.IsMainThread)
            {
                AwaiterExtensions.RunCoroutine(Run(continuation));
            }
            else
            {
                var awaiter = this;
                AwaiterExtensions.Queue(() => AwaiterExtensions.RunCoroutine(awaiter.Run(continuation)));
            }
        }

        private IEnumerator Run(Action continuation)
        {
            yield return coroutine;
            continuation();
        }

        public bool IsCompleted => false;

        public void GetResult() { }
    }
}
