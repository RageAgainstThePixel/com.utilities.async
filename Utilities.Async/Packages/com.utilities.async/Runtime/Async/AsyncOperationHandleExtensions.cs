// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_ADDRESSABLES

using UnityEngine.ResourceManagement.AsyncOperations;

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

        public static CoroutineAwaiter<AsyncOperationHandle> GetAwaiter(this AsyncOperationHandle instruction)
            => new(instruction);

        public static CoroutineAwaiter<T> GetAwaiter<T>(this AsyncOperationHandle<T> instruction)
            => new(instruction);
    }
}

#endif // UNITY_ADDRESSABLES
