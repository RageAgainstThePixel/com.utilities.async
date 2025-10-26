// Licensed under the MIT License. See LICENSE in the project root for license information.

using JetBrains.Annotations;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Utilities.Async
{
    public readonly struct YieldInstructionAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        private static readonly ConcurrentQueue<YieldInstructionWork<object>> pool = new();
        private readonly YieldInstructionWork<object> work;

        public YieldInstructionAwaiter(object instruction)
            => work = YieldInstructionWork<object>.Rent(instruction);

        public bool IsCompleted => work.IsCompleted;

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => work.RegisterContinuation(continuation);

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
                YieldInstructionWork<object>.Return(work);
            }
        }
    }

    public readonly struct YieldInstructionAwaiter<T> : ICriticalNotifyCompletion, IAwaiter
    {
        private static readonly ConcurrentQueue<YieldInstructionWork<T>> pool = new();
        private readonly YieldInstructionWork<T> work;

        public YieldInstructionAwaiter(object instruction)
            => work = YieldInstructionWork<T>.Rent(instruction);

        public bool IsCompleted => work.IsCompleted;

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => work.RegisterContinuation(continuation);

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
                YieldInstructionWork<T>.Return(work);
            }
        }
    }
}
