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
            if (handle.IsValid() &&
                handle.OperationException != null)
            {
                throw handle.OperationException;
            }
        }

        public static SimpleCoroutineAwaiter<AsyncOperationHandle> GetAwaiter(this AsyncOperationHandle instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<AsyncOperationHandle>();
            RunOnUnityScheduler(() => RunCoroutine(new AsyncOperationWrapper<AsyncOperationHandle>(instruction, awaiter).Run()));
            return awaiter;
        }

        public static SimpleCoroutineAwaiter<T> GetAwaiter<T>(this AsyncOperationHandle<T> instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<T>();
            RunOnUnityScheduler(() => RunCoroutine(new AsyncOperationWrapper<T>(instruction, awaiter).Run()));
            return awaiter;
        }

        internal class AsyncOperationWrapper<T> : CoroutineWrapper<T>
        {
            protected override bool CheckStatus(IEnumerator topWorker, out object nextWorker)
            {
                nextWorker = default;
                switch (topWorker)
                {
                    case AsyncOperationHandle operationHandle:
                        if (operationHandle.IsValid())
                        {
                            nextWorker = topWorker.Current;
                        }
                        operationHandle.TryThrowException();
                        return operationHandle.IsDone;
                    case AsyncOperationHandle<T> operationHandle:
                        if (operationHandle.IsValid())
                        {
                            nextWorker = topWorker.Current;
                        }
                        operationHandle.TryThrowException();
                        return operationHandle.IsDone;
                    default:
                        return base.CheckStatus(topWorker, out nextWorker);
                }
            }

            public AsyncOperationWrapper(AsyncOperationHandle operationHandle, SimpleCoroutineAwaiter<T> awaiter)
                : base(operationHandle, awaiter)
            {
            }
        }
    }
}

#endif // UNITY_ADDRESSABLES
