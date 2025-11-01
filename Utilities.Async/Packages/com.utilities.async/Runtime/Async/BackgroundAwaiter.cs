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
            UnityEngine.Debug.LogWarning($"{nameof(BackgroundAwaiter)} not supported for {nameof(UnityEngine.RuntimePlatform.WebGLPlayer)}. Continued on MainThread");
            continuation(); // WebGL does not support threads, so we just invoke the continuation directly.
#else
            ThreadPool.UnsafeQueueUserWorkItem(waitCallback, continuation);
#endif
        }

        private static WaitCallback waitCallback = WaitCallback;

        private static void WaitCallback(object state)
        {
            if (state is Action action)
            {
                action.Invoke();
            }
        }
    }
}
