// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_ADDRESSABLES

using System.Collections;
using UnityEngine.ResourceManagement.AsyncOperations;
using static Utilities.Async.AwaiterExtensions;

namespace Utilities.Async.Addressables
{
    public static class AsyncOperationHandleExtensions
    {
        public static SimpleCoroutineAwaiter<T> GetAwaiter<T>(this AsyncOperationHandle<T> instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<T>();
            var enumerator = AsyncOperationRequest(awaiter, instruction);
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

        private static IEnumerator AsyncOperationRequest<T>(SimpleCoroutineAwaiter<T> awaiter, AsyncOperationHandle<T> instruction)
        {
            yield return instruction;
            awaiter.Complete(instruction.Result, instruction.OperationException);
        }
    }
}

#endif // UNITY_ADDRESSABLES
