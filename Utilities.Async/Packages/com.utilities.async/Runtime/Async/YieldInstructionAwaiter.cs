// Licensed under the MIT License. See LICENSE in the project root for license information.

using JetBrains.Annotations;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Utilities.Async
{
    public readonly struct YieldInstructionAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly YieldInstructionWork<object> work;
        private readonly ValueTaskAwaiter<object> awaiter;

        public YieldInstructionAwaiter(object instruction)
        {
            work = YieldInstructionWork<object>.Rent(instruction);
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
                YieldInstructionWork<object>.Return(work);
            }
        }
    }

    public readonly struct YieldInstructionAwaiter<T> : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly YieldInstructionWork<T> work;
        private readonly ValueTaskAwaiter<T> awaiter;

        public YieldInstructionAwaiter(object instruction)
        {
            work = YieldInstructionWork<T>.Rent(instruction);
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
                YieldInstructionWork<T>.Return(work);
            }
        }
    }
}
