// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using UnityEngine;

namespace Utilities.Async
{
    internal sealed class YieldInstructionWrapper<T> : IEnumerator
    {
        private int state;
        private object instruction;
        private YieldInstructionTaskSource<T> taskSource;

        public YieldInstructionWrapper(YieldInstructionTaskSource<T> owner)
            => taskSource = owner;

        public object Current => state == 1 ? instruction : null;

        public bool MoveNext()
        {
            if (taskSource.IsCompleted)
            {
                InstructionComplete();
            }

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
                    work.CompleteWork(asyncOperation);
                    break;
#endif
                default:
                    taskSource.CompleteWork(instruction);
                    break;
            }
        }

        public void Clear()
        {
            instruction = null;
            state = 0;
        }

        public void Cancel()
        {
            instruction = null;
            state = 2;
        }
    }
}
