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
