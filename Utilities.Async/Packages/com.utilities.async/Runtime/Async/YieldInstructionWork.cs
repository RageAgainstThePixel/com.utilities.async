// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;

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
}
