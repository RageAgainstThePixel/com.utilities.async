// Licensed under the MIT License. See LICENSE in the project root for license information.

using JetBrains.Annotations;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Utilities.Async
{
    public readonly struct YieldInstructionAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly YieldInstructionTaskSource<object> taskSource;
        private readonly ValueTaskAwaiter<object> awaiter;

        public YieldInstructionAwaiter(object instruction)
        {
            taskSource = YieldInstructionTaskSource<object>.Rent(instruction);
            awaiter = new ValueTask<object>(taskSource, taskSource.Version).GetAwaiter();
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
                YieldInstructionTaskSource<object>.Return(taskSource);
            }
        }
    }

    public readonly struct YieldInstructionAwaiter<T> : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly YieldInstructionTaskSource<T> taskSource;
        private readonly ValueTaskAwaiter<T> awaiter;

        public YieldInstructionAwaiter(object instruction)
        {
            taskSource = YieldInstructionTaskSource<T>.Rent(instruction);
            awaiter = new ValueTask<T>(taskSource, taskSource.Version).GetAwaiter();
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
                YieldInstructionTaskSource<T>.Return(taskSource);
            }
        }
    }
}
