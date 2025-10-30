// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Utilities.Async
{
    internal sealed class CoroutineWrapper<T> : IEnumerator
    {
        private readonly CoroutineTaskSource<T> taskSource;
        private readonly Stack<IEnumerator> processStack;

        public CoroutineWrapper(CoroutineTaskSource<T> owner)
        {
            processStack = new Stack<IEnumerator>();
            taskSource = owner;
        }

        private static bool CheckStatus(IEnumerator worker, out object next)
        {
            var isDone = !worker.MoveNext();
            next = isDone ? worker.Current : null;
            return isDone;
        }

        public object Current { get; private set; }

        public bool MoveNext()
        {
            while (true)
            {
                if (processStack.Count == 0 || taskSource.IsComplete) { return false; }

                var topWorker = processStack.Peek();
                bool isDone;
                object nextWorker;

                try
                {
                    isDone = CheckStatus(topWorker, out nextWorker);
                }
                catch (Exception e)
                {
                    taskSource.FailWithException(processStack.GenerateExceptionTrace(e));
                    return false;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        taskSource.CompleteWork(nextWorker ?? topWorker.Current);
                        return false;
                    }

                    // continue to process the next item on stack
                    continue;
                }

                // If current is another IEnumerator, push it to the stack and continue
                if (topWorker.Current is IEnumerator item)
                {
                    processStack.Push(item);
                    continue;
                }

                // Otherwise yield the current value to Unity's coroutine runner
                Current = topWorker.Current;
                return true;
            }
        }

        public void Reset()
            => throw new NotSupportedException();

        public void Initialize(IEnumerator coroutine)
        {
            processStack.Clear();
            processStack.Push(coroutine);
            Current = null;
        }

        public void Clear()
        {
            processStack.Clear();
            Current = null;
        }

        public void Cancel()
        {
            // Stop further processing by clearing the stack and current value.
            processStack.Clear();
            Current = null;
        }
    }
}
