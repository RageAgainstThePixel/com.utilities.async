// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Utilities.Async
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    internal class CoroutineWrapper<T> : IEnumerator
    {
        private readonly CoroutineWork<T> owner;
        private readonly Stack<IEnumerator> processStack;

        public CoroutineWrapper(CoroutineWork<T> coroutineOwner)
        {
            processStack = new Stack<IEnumerator>();
            owner = coroutineOwner;
        }

        protected virtual bool CheckStatus(IEnumerator worker, out object next)
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
                if (processStack.Count == 0) { return false; }

                var topWorker = processStack.Peek();
                bool isDone;
                object nextWorker;

                try
                {
                    isDone = CheckStatus(topWorker, out nextWorker);
                }
                catch (Exception e)
                {
                    owner.FailWithException(processStack.GenerateExceptionTrace(e));
                    return false;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        owner.CompleteWork(nextWorker ?? topWorker.Current);
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
