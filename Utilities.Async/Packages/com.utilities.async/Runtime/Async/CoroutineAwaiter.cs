// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using JetBrains.Annotations;

namespace Utilities.Async
{
    public readonly struct CoroutineAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly CoroutineWork work;

        public CoroutineAwaiter(object instruction) : this()
            => work = CoroutineWork.Rent(this, instruction);

        public bool IsCompleted => work.IsCompleted;

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => work.Continuation = continuation;

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult()
        {
            try
            {
                if (work.Exception != null)
                {
                    ExceptionDispatchInfo.Capture(work.Exception).Throw();
                }
            }
            finally
            {
                CoroutineWork.Return(work);
            }
        }
    }

    public readonly struct CoroutineAwaiter<T> : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly CoroutineWork<T> work;

        public CoroutineAwaiter(object instruction) : this()
            => work = CoroutineWork<T>.Rent(this, instruction);

        public bool IsCompleted => work.IsCompleted;

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => work.Continuation = continuation;

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetResult()
        {
            try
            {
                if (work.Exception != null)
                {
                    ExceptionDispatchInfo.Capture(work.Exception).Throw();
                }

                return (T)work.Result;
            }
            finally
            {
                CoroutineWork<T>.Return(work);
            }
        }
    }
}
