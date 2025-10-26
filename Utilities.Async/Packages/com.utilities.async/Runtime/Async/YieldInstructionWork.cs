// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace Utilities.Async
{
    internal sealed class YieldInstructionWork<T> : IWorkItem
    {
        private static readonly ConcurrentQueue<YieldInstructionWork<T>> pool = new();

        private Action continuation;
        private YieldInstructionWrapper instructionWrapper = new();

        private YieldInstructionWork() { }

        public static YieldInstructionWork<T> Rent(object instruction)
        {
            if (instruction == null)
            {
                throw new InvalidOperationException($"{nameof(instruction)} cannot be null!");
            }

            if (!pool.TryDequeue(out var work))
            {
                work = new YieldInstructionWork<T>();
            }

            work.Exception = null;

            switch (instruction)
            {
#if UNITY_ASSET_BUNDLES
                case AssetBundleRequest assetBundleRequest:
                    work.instructionWrapper.Initialize(work.AssetBundleRequest(assetBundleRequest));
                    break;
                case AssetBundleCreateRequest assetBundleCreateRequest:
                    work.instructionWrapper.Initialize(work.AssetBundleCreateRequest(assetBundleCreateRequest));
                    break;
#endif
                case ResourceRequest resourceRequest:
                    work.instructionWrapper.Initialize(work.ResourceRequest(resourceRequest));
                    break;
#if !UNITY_2023_1_OR_NEWER
                case AsyncOperation asyncOperation:
                    work.instructionWrapper.Initialize(work.ReturnAsyncOperation(asyncOperation));
                    break;
#endif
                default:
                    work.instructionWrapper.Initialize(work.ReturnVoid(instruction));
                    break;
            }

            SyncContextUtility.RunOnUnityThread(work.StartWorkCoroutineRunner);
            return work;
        }

        public static void Return(YieldInstructionWork<T> work)
        {
            work.IsCompleted = false;
            work.Exception = null;
            work.Result = null;
            work.instructionWrapper.Clear();
            pool.Enqueue(work);
        }

        private void StartWorkCoroutineRunner()
            => AwaiterExtensions.RunCoroutine(instructionWrapper);

#if UNITY_ASSET_BUNDLES

        private IEnumerator AssetBundleRequest(AssetBundleRequest instruction)
        {
            yield return instruction;
            CompleteWork(instruction.asset);
        }

        private IEnumerator AssetBundleCreateRequest(AssetBundleCreateRequest instruction)
        {
            yield return instruction;
            CompleteWork(instruction.assetBundle);
        }

#endif // UNITY_ASSET_BUNDLES

        private IEnumerator ResourceRequest(ResourceRequest instruction)
        {
            yield return instruction;
            CompleteWork(instruction.asset);
        }

#if !UNITY_2023_1_OR_NEWER

        private IEnumerator ReturnAsyncOperation(AsyncOperation instruction)
        {
            yield return instruction;
            CompleteWork(instruction);
        }

#endif // !UNITY_2023_1_OR_NEWER

        private IEnumerator ReturnVoid(object instruction)
        {
            yield return instruction;
            CompleteWork(instruction);
        }

        public bool IsCompleted { get; private set; }

        public object Result { get; private set; }

        public Exception Exception { get; private set; }


        public void RegisterContinuation(Action action)
            => continuation = action;

        private void CompleteWork(object result)
        {
            IsCompleted = true;
            Result = result;
            SyncContextUtility.RunOnUnityThread(continuation);
        }
    }

    internal sealed class YieldInstructionWrapper : IEnumerator
    {
        private int state;
        private object instruction;

        public object Current => state == 1 ? instruction : null;

        public bool MoveNext()
        {
            switch (state)
            {
                case 0:
                    state = 1;
                    return true;
                case 1:
                    state = 2;
                    instruction = null;
                    return false;
                default:
                    return false;
            }
        }

        public void Reset()
            => throw new NotSupportedException();

        public void Initialize(object value)
        {
            instruction = value;
            state = 0;
        }

        public void Clear()
        {
            instruction = null;
            state = 0;
        }
    }
}
