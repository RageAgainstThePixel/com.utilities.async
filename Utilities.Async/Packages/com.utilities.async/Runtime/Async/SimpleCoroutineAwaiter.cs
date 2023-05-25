using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using UnityEngine;
using static Utilities.Async.AwaiterExtensions;

namespace Utilities.Async
{
    /// <summary>
    /// Processes Coroutine and notifies completion.
    /// </summary>
    public class SimpleCoroutineAwaiter : INotifyCompletion
    {
        private Exception exception;
        private Action continuation;

        public bool IsCompleted { get; private set; }

        public void GetResult()
        {
            Debug.Assert(IsCompleted);

            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        public void Complete(Exception e = null)
        {
            Debug.Assert(!IsCompleted);

            IsCompleted = true;
            exception = e;

            // Always trigger the continuation on the unity thread
            // when awaiting on unity yield instructions.
            if (continuation != null)
            {
                RunOnUnityScheduler(continuation);
            }
        }

        void INotifyCompletion.OnCompleted(Action notifyContinuation)
        {
            Debug.Assert(continuation == null);
            Debug.Assert(!IsCompleted);

            continuation = notifyContinuation;
        }
    }

    /// <summary>
    /// Processes Coroutine and notifies completion with result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public class SimpleCoroutineAwaiter<T> : INotifyCompletion
    {
        private Exception exception;
        private Action continuation;
        private T result;

        public bool IsCompleted { get; private set; }

        public T GetResult()
        {
            Debug.Assert(IsCompleted);

            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return result;
        }

        public void Complete(T taskResult, Exception e = null)
        {
            Debug.Assert(!IsCompleted);

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

        void INotifyCompletion.OnCompleted(Action notifyContinuation)
        {
            Debug.Assert(continuation == null);
            Debug.Assert(!IsCompleted);
            continuation = notifyContinuation;
        }
    }
}
