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

        private readonly Action runner;

        private YieldInstructionWrapper<T> instructionWrapper;

        private Action continuation;

        public bool IsCompleted { get; private set; }

        public object Result { get; private set; }

        public Exception Exception { get; private set; }

        private YieldInstructionWork()
        {
            instructionWrapper = new YieldInstructionWrapper<T>(this);
            runner = () => AwaiterExtensions.RunCoroutine(instructionWrapper);
        }

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
            work.instructionWrapper.Initialize(instruction);
            SyncContextUtility.RunOnUnityThread(work.runner);
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

        public void RegisterContinuation(Action action)
            => continuation = action;

        public void CompleteWork(object result)
        {
            IsCompleted = true;
            Result = result;
            SyncContextUtility.RunOnUnityThread(continuation);
        }
    }

    internal sealed class YieldInstructionWrapper<T> : IEnumerator
    {
        private int state;
        private object instruction;
        private YieldInstructionWork<T> work;

        public YieldInstructionWrapper(YieldInstructionWork<T> owner)
            => work = owner;

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
                    InstructionComplete();
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

        private void InstructionComplete()
        {
            switch (instruction)
            {
#if UNITY_ASSET_BUNDLES
                case AssetBundleRequest assetBundleRequest:
                    work.CompleteWork(assetBundleRequest.asset);
                    break;
                case AssetBundleCreateRequest assetBundleCreateRequest:
                    work.CompleteWork(assetBundleCreateRequest.assetBundle);
                    break;
#endif
                case ResourceRequest resourceRequest:
                    work.CompleteWork(resourceRequest.asset);
                    break;
#if !UNITY_2023_1_OR_NEWER
                case AsyncOperation asyncOperation:
                    work.CompleteWork(asyncOperation);
                    break;
#endif
                default:
                    work.CompleteWork(instruction);
                    break;
            }
        }

        public void Clear()
        {
            instruction = null;
            state = 0;
        }
    }
}
