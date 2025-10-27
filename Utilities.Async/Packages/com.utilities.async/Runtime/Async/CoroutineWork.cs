// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_ADDRESSABLES
using UnityEngine.ResourceManagement.AsyncOperations;
using Utilities.Async.Addressables;
#endif

namespace Utilities.Async
{
    internal sealed class CoroutineWork<T> : IWorkItem
#if UNITY_EDITOR
        , IEditorCancelable
#endif
    {
        private static readonly ConcurrentQueue<CoroutineWork<T>> pool = new();

        private readonly CoroutineWrapper coroutineWrapper = new();

#if UNITY_EDITOR
        private IDisposable editorCancellationRegistration;
#endif

        private CoroutineWork() { }

        public static CoroutineWork<T> Rent(object instruction)
        {
            if (!pool.TryDequeue(out var work))
            {
                work = new CoroutineWork<T>();
                void StartWorkCoroutineRunner() => AwaiterExtensions.RunCoroutine(work.Run());
                SyncContextUtility.RunOnUnityThread(StartWorkCoroutineRunner);
            }

            work.Result = null;
            work.Exception = null;
            work.IsCompleted = false;

            if (instruction is IEnumerator enumerator)
            {
                work.processStack.Push(enumerator);
            }
            else
            {
                work.coroutineWrapper.Initialize(instruction);
                work.processStack.Push(work.coroutineWrapper);
            }

#if UNITY_EDITOR
            work.editorCancellationRegistration?.Dispose();
            work.editorCancellationRegistration = EditorPlayModeCancellation.Register(work);
#endif

            return work;
        }

        public static void Return(CoroutineWork<T> work)
        {
            work.IsCompleted = false;
            work.Exception = null;
            work.Result = null;
            work.processStack.Clear();
            work.coroutineWrapper.Clear();
#if UNITY_EDITOR
            work.editorCancellationRegistration?.Dispose();
            work.editorCancellationRegistration = null;
#endif
            pool.Enqueue(work);
        }

        private readonly Stack<IEnumerator> processStack = new();

        private Action continuation;

        public Exception Exception { get; private set; }

        public bool IsCompleted { get; private set; }

        public object Result { get; private set; }

        private static bool CheckStatus(IEnumerator worker, out object next)
        {
            next = null;
#if UNITY_ADDRESSABLES
            switch (worker)
            {
                case AsyncOperationHandle operationHandle:
                    if (operationHandle.IsValid())
                    {
                        next = worker.Current;
                    }
                    operationHandle.TryThrowException();
                    return operationHandle.IsDone;
                case AsyncOperationHandle<T> operationHandle:
                    if (operationHandle.IsValid())
                    {
                        next = worker.Current;
                    }
                    operationHandle.TryThrowException();
                    return operationHandle.IsDone;
            }
#endif
            var isDone = !worker.MoveNext();
            next = isDone ? worker.Current : null;
            return isDone;
        }

        private IEnumerator Run()
        {
            while (true)
            {
                if (IsCompleted)
                {
                    yield return null;
                    continue;
                }

                if (processStack.Count == 0)
                {
                    IsCompleted = true;
                    yield return null;
                    continue;
                }

                bool isDone;
                object currentWorker = null;
                var topWorker = processStack.Peek();

                try
                {
                    isDone = CheckStatus(topWorker, out var nextWorker);

                    if (isDone)
                    {
                        currentWorker = nextWorker;
                    }
                }
                catch (Exception e)
                {
                    // The IEnumerators we have in the process stack do not tell us the
                    // actual names of the coroutine methods, but it does tell us the objects
                    // that the IEnumerators are associated with, so we can at least try
                    // adding that to the exception output
                    Exception = processStack.GenerateExceptionTrace(e);
                    InvokeContinuation();
                    continue;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        try
                        {
                            Result = currentWorker ?? topWorker.Current;
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }

                        InvokeContinuation();
                        continue;
                    }
                }

                // We could just yield return nested IEnumerator's here, but we choose to do
                // our own handling here so that we can catch exceptions in nested coroutines
                // instead of just top level coroutine
                if (topWorker.Current is IEnumerator item)
                {
                    processStack.Push(item);
                }
                else
                {
                    // Return the current value to the unity engine so it can handle things like
                    // WaitForSeconds, WaitToEndOfFrame, etc.
                    yield return topWorker.Current;
                }
            }
            // ReSharper disable once IteratorNeverReturns
        }

        public void RegisterContinuation(Action action)
            => continuation = action;

        private void InvokeContinuation()
        {
            IsCompleted = true;

            try
            {
                continuation?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

#if UNITY_EDITOR
        void IEditorCancelable.CancelFromEditor()
        {
            if (IsCompleted) { return; }
            editorCancellationRegistration?.Dispose();
            editorCancellationRegistration = null;
            coroutineWrapper.Cancel();
            processStack.Clear();
            Result = null;
            Exception ??= new OperationCanceledException(EditorPlayModeCancellation.CancellationMessage);
            InvokeContinuation();
        }
#endif
    }
}
