// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using UnityEngine;

namespace Utilities.Async
{
    internal sealed class YieldInstructionWorker<T> : IEnumerator, IDisposable
    {
        private readonly YieldInstructionTaskSource<T> taskSource;

        private object current;
        private object instruction;

        private bool isRunning;
        private bool isAwaitingCompletion;
        private bool hasPendingInstruction;

        private CoroutineWrapper? runner;

        public YieldInstructionWorker(YieldInstructionTaskSource<T> owner)
            => taskSource = owner;

        public void Initialize(object value)
        {
            instruction = value;
            hasPendingInstruction = true;
            isAwaitingCompletion = false;
            current = null;
            EnsureRunning();
        }

        internal void EnsureRunning()
        {
            if (isRunning) { return; }
            isRunning = true;
            SyncContextUtility.RunOnUnityThread(() =>
            {
                runner = AwaiterExtensions.RunCoroutine(this);
            });
        }

        private void InstructionComplete()
        {
            switch (instruction)
            {
#if UNITY_ASSET_BUNDLES
                case AssetBundleRequest assetBundleRequest:
                    taskSource.CompleteWork(assetBundleRequest.asset);
                    break;
                case AssetBundleCreateRequest assetBundleCreateRequest:
                    taskSource.CompleteWork(assetBundleCreateRequest.assetBundle);
                    break;
#endif
                case ResourceRequest resourceRequest:
                    taskSource.CompleteWork(resourceRequest.asset);
                    break;
#if !UNITY_2023_1_OR_NEWER
                case AsyncOperation asyncOperation:
                    taskSource.CompleteWork(asyncOperation);
                    break;
#endif
                default:
                    taskSource.CompleteWork(instruction);
                    break;
            }
        }

        #region IEnumerator

        object IEnumerator.Current => current;

        bool IEnumerator.MoveNext()
        {
            if (!hasPendingInstruction)
            {
                current = null;
                return true;
            }

            if (!isAwaitingCompletion)
            {
                isAwaitingCompletion = true;
                current = instruction;
                return true;
            }

            isAwaitingCompletion = false;
            hasPendingInstruction = false;
            InstructionComplete();
            instruction = null;
            current = null;
            return true;
        }

        public void Reset()
        {
            instruction = null;
            hasPendingInstruction = false;
            isAwaitingCompletion = false;
            current = null;
        }

        #endregion IEnumerator

        public void Dispose()
            => runner?.StopCoroutine();
    }
}
