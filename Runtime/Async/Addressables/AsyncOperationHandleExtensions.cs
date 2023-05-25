// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_ADDRESSABLES

using System.Collections;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Utilities.Async.AwaiterExtensions;

namespace Utilities.Async.Addressables
{
    public static class AsyncOperationHandleExtensions
    {
        public static void TryThrowException<T>(this AsyncOperationHandle<T> handle)
            => TryThrowException((AsyncOperationHandle)handle);

        public static void TryThrowException(this AsyncOperationHandle handle)
        {
            if (handle.OperationException != null)
            {
                throw handle.OperationException;
            }
        }

        public static SimpleCoroutineAwaiter<AsyncOperationHandle> GetAwaiter(this AsyncOperationHandle instruction)
            => GetAwaiterReturnSelf(instruction);

        public static SimpleCoroutineAwaiter<T> GetAwaiter<T>(this AsyncOperationHandle<T> instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<T>();
            var enumerator = new AsyncOperationWrapper<T>(instruction, awaiter).Run();
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

        internal class AsyncOperationWrapper<T> : CoroutineWrapper<T>
        {
            protected override bool CheckStatus(IEnumerator topWorker)
            {
                topWorker.TryThrowException();
                return topWorker.IsDone();
            }

            public AsyncOperationWrapper(IEnumerator coroutine, SimpleCoroutineAwaiter<T> awaiter)
                : base(coroutine, awaiter)
            {
            }
        }

        internal static void TryThrowException(this IEnumerator enumerator)
        {
            if (enumerator is AsyncOperationHandle handle)
            {
                handle.TryThrowException();
            }
        }

        internal static bool IsDone(this IEnumerator enumerator)
        {
            if (enumerator is AsyncOperationHandle handle)
            {
                return handle.IsDone;
            }

            return !enumerator.MoveNext();
        }
    }
}

#endif // UNITY_ADDRESSABLES
