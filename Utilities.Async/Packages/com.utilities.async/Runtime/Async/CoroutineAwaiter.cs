// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Utilities.Async
{
    public readonly struct CoroutineAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly CoroutineWork work;

        public CoroutineAwaiter(object instruction) : this()
            => work = CoroutineWork.Rent(instruction);

        private CoroutineWork Work => work ?? throw new InvalidOperationException("CoroutineAwaiter is not initialized.");

        public bool IsCompleted => Work.Task.IsCompleted;

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => Work.RegisterContinuation(continuation);

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult()
        {
            var activeWork = Work;

            try
            {
                activeWork.Task.GetAwaiter().GetResult();
            }
            finally
            {
                CoroutineWork.Return(activeWork);
            }
        }
    }

    public readonly struct CoroutineAwaiter<T> : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly CoroutineWork<T> work;

        public CoroutineAwaiter(object instruction) : this()
            => work = CoroutineWork<T>.Rent(instruction);

        private CoroutineWork<T> Work => work ?? throw new InvalidOperationException("CoroutineAwaiter is not initialized.");

        public bool IsCompleted => Work.Task.IsCompleted;

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => Work.RegisterContinuation(continuation);

        [UsedImplicitly]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetResult()
        {
            var activeWork = Work;

            try
            {
                return activeWork.Task.GetAwaiter().GetResult();
            }
            finally
            {
                CoroutineWork<T>.Return(activeWork);
            }
        }
    }
}
