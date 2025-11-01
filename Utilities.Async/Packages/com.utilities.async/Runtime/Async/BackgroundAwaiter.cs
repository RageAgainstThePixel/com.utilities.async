// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Utilities.Async
{
    public readonly struct BackgroundAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        private readonly BackgroundWork work;
        private readonly ValueTaskAwaiter awaiter;

        private BackgroundAwaiter(BackgroundWork work)
        {
            this.work = work;
            awaiter = new ValueTask(work, work.Version).GetAwaiter();
        }

        public static BackgroundAwaiter Run()
            => new(BackgroundWork.Rent());

        public bool IsCompleted => awaiter.IsCompleted;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult()
        {
            try
            {
                awaiter.GetResult();
            }
            finally
            {
                BackgroundWork.Return(work);
            }
        }

        public void OnCompleted(Action continuation)
            => awaiter.OnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
            => awaiter.UnsafeOnCompleted(continuation);
    }
}
