// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Utilities.Async
{
    internal sealed class CoroutineWorker<T> : IEnumerator, IDisposable
    {
        private readonly Stack<IEnumerator> processStack;
        private readonly CoroutineTaskSource<T> taskSource;

        private object current;

        private bool isRunning;

        private CoroutineWrapper? runner;

        public CoroutineWorker(CoroutineTaskSource<T> owner)
        {
            taskSource = owner;
            processStack = new Stack<IEnumerator>();
        }

        public void Initialize(IEnumerator coroutine)
        {
            processStack.Clear();
            processStack.Push(coroutine);
            current = null;
            EnsureRunning();
        }

        private void EnsureRunning()
        {
            if (isRunning) { return; }
            isRunning = true;
            SyncContextUtility.RunOnUnityThread(() =>
            {
                runner = AwaiterExtensions.RunCoroutine(this);
            });
        }

        #region IEnumerator

        object IEnumerator.Current => current;

        bool IEnumerator.MoveNext()
        {
            while (true)
            {
                if (processStack.Count == 0)
                {
                    current = null;
                    return true;
                }

                if (taskSource.IsComplete)
                {
                    processStack.Clear();
                    current = null;
                    return true;
                }

                var topWorker = processStack.Peek();
                bool isDone;
                object nextWorker;

                try
                {
                    isDone = CheckStatus(topWorker, out nextWorker);

                    static bool CheckStatus(IEnumerator worker, out object next)
                    {
                        var isDone = !worker.MoveNext();
                        next = isDone ? worker.Current : null;

                        return isDone;
                    }
                }
                catch (Exception e)
                {
                    var wrappedException = processStack.GenerateExceptionTrace(e);
                    processStack.Clear();
                    taskSource.FailWithException(wrappedException);
                    current = null;
                    return true;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        taskSource.CompleteWork(nextWorker ?? topWorker.Current);
                        current = null;
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
                current = topWorker.Current;
                return true;
            }
        }

        public void Reset()
        {
            // Stop further processing by clearing the stack and current value.
            processStack.Clear();
            current = null;
        }

        #endregion IEnumerator

        public void Dispose()
            => runner?.StopCoroutine();
    }
}
