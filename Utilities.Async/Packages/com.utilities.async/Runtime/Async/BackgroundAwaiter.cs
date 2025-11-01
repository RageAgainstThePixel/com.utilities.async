// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Utilities.Async
{
    public readonly struct BackgroundAwaiter : ICriticalNotifyCompletion, IAwaiter
    {
        public bool IsCompleted => !SyncContextUtility.IsMainThread;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult() { }

        public void OnCompleted(Action continuation)
            => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
        {
#if UNITY_WEBGL
            continuation.Invoke();
#else
            ThreadPool.UnsafeQueueUserWorkItem(waitCallback, continuation);
#endif
        }

        private static readonly WaitCallback waitCallback = WaitCallback;

        private static void WaitCallback(object state)
        {
            if (state is Action continuation)
            {
                continuation.Invoke();
            }
        }
    }
}
