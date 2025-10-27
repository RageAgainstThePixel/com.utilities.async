// Licensed under the MIT License. See LICENSE in the project root for license information.

using JetBrains.Annotations;
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Utilities.Async
{
    public readonly struct CoroutineAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly CoroutineWork<object> work;
        private readonly ValueTaskAwaiter<object> awaiter;

        public CoroutineAwaiter(IEnumerator instruction) : this()
        {
            work = CoroutineWork<object>.Rent(instruction);
            awaiter = new ValueTask<object>(work, work.Version).GetAwaiter();
        }

        public bool IsCompleted => awaiter.IsCompleted;

        public void OnCompleted(Action continuation)
            => awaiter.OnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => awaiter.UnsafeOnCompleted(continuation);

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetResult()
        {
            try
            {
                return awaiter.GetResult();
            }
            finally
            {
                CoroutineWork<object>.Return(work);
            }
        }
    }

    public readonly struct CoroutineAwaiter<T> : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly CoroutineWork<T> work;
        private readonly ValueTaskAwaiter<T> awaiter;

        public CoroutineAwaiter(IEnumerator instruction) : this()
        {
            work = CoroutineWork<T>.Rent(instruction);
            awaiter = new ValueTask<T>(work, work.Version).GetAwaiter();
        }

        public bool IsCompleted => awaiter.IsCompleted;

        public void OnCompleted(Action continuation)
            => awaiter.OnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => awaiter.UnsafeOnCompleted(continuation);

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetResult()
        {
            try
            {
                return awaiter.GetResult();
            }
            finally
            {
                CoroutineWork<T>.Return(work);
            }
        }
    }
}
