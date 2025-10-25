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
    internal class CoroutineWork
    {
        private static readonly ConcurrentQueue<CoroutineWork> pool = new();

        private CoroutineWork() { }

        public static CoroutineWork Rent(CoroutineAwaiter awaiter, object instruction)
        {
            if (!pool.TryDequeue(out var work))
            {
                work = new CoroutineWork();
                SyncContextUtility.RunOnUnityThread(() => AwaiterExtensions.RunCoroutine(work.Run()));
            }

            work.IsCompleted = false;

            if (instruction is IEnumerator enumerator)
            {
                work.processStack.Push(enumerator);
            }
            else
            {
                work.processStack.Push(ReturnVoid(instruction));
            }

            work.Awaiter = awaiter;
            work.Exception = null;
            return work;
        }

        private static IEnumerator ReturnVoid(object instruction)
        {
            yield return instruction;
        }

        public static void Return(CoroutineWork work)
        {
            work.Awaiter = null;
            work.Exception = null;
            work.IsCompleted = false;
            work.processStack.Clear();
            pool.Enqueue(work);
        }

        private readonly Stack<IEnumerator> processStack = new();

        public Exception Exception { get; private set; }

        public CoroutineAwaiter? Awaiter { get; private set; }

        public bool IsCompleted { get; private set; }

        private IEnumerator Run()
        {
            while (true)
            {
                if (IsCompleted)
                {
                    yield return null;
                    continue;
                }

                if (Awaiter == null)
                {
                    IsCompleted = false;
                    yield return null;
                    continue;
                }

                if (processStack.Count == 0)
                {
                    IsCompleted = true;
                    yield return null;
                    continue;
                }

                var topWorker = processStack.Peek();
                bool isDone;

                try
                {
                    isDone = !topWorker.MoveNext();
                }
                catch (Exception e)
                {
                    // The IEnumerators we have in the process stack do not tell us the
                    // actual names of the coroutine methods, but it does tell us the objects
                    // that the IEnumerators are associated with, so we can at least try
                    // adding that to the exception output
                    Exception = processStack.GenerateExceptionTrace(e);
                    IsCompleted = true;
                    continue;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        IsCompleted = true;
                        continue;
                    }
                }

                // We could just yield return nested IEnumerator's here but we choose to do
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
    }

    internal sealed class CoroutineWork<T>
    {
        private static readonly ConcurrentQueue<CoroutineWork<T>> pool = new();

        private CoroutineWork() { }

        public static CoroutineWork<T> Rent(CoroutineAwaiter<T> awaiter, object instruction)
        {
            if (!pool.TryDequeue(out var work))
            {
                work = new CoroutineWork<T>();
                SyncContextUtility.RunOnUnityThread(() => AwaiterExtensions.RunCoroutine(work.Run()));
            }

            work.Result = default;
            work.Exception = null;
            work.IsCompleted = false;

            if (instruction is IEnumerator enumerator)
            {
                work.processStack.Push(enumerator);
            }
            else
            {
                work.instruction = instruction;
                work.processStack.Push(ReturnResult(instruction));
            }

            work.Awaiter = awaiter;
            return work;
        }

        private static IEnumerator ReturnResult(object instruction)
        {
            yield return instruction;
        }

        public static void Return(CoroutineWork<T> work)
        {
            work.Awaiter = null;
            work.Exception = null;
            work.Result = default;
            work.instruction = default;
            work.IsCompleted = false;
            work.processStack.Clear();
            pool.Enqueue(work);
        }

        private readonly Stack<IEnumerator> processStack = new();

        private object instruction;

        public Exception Exception { get; private set; }

        public CoroutineAwaiter<T>? Awaiter { get; private set; }

        public bool IsCompleted { get; private set; }

        public object Result { get; private set; }

        private static bool CheckStatus(IEnumerator worker, out object next)
        {
            next = default;
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
            next = isDone ? worker.Current : default;
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

                if (Awaiter == null)
                {
                    IsCompleted = false;
                    yield return null;
                    continue;
                }

                if (processStack.Count == 0)
                {
                    IsCompleted = true;
                    yield return null;
                    continue;
                }

                var topWorker = processStack.Peek();
                bool isDone;
                object currentWorker = default;

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
                    IsCompleted = true;
                    continue;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        try
                        {
                            switch (instruction)
                            {
#if UNITY_ASSET_BUNDLES
                                case AssetBundleCreateRequest assetBundleCreateRequest:
                                    Result = assetBundleCreateRequest.assetBundle;
                                    break;
                                case AssetBundleRequest assetBundleRequest:
                                    Result = assetBundleRequest.asset;
                                    break;
#endif // UNITY_ASSET_BUNDLES
                                case ResourceRequest resourceRequest:
                                    Result = resourceRequest.asset;
                                    break;
#if !UNITY_2023_1_OR_NEWER
                                case AsyncOperation asyncOperation:
                                    Result = asyncOperation;
                                    break;
#endif
                                default:
                                    Result = currentWorker ?? topWorker.Current;
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }

                        IsCompleted = true;
                        continue;
                    }
                }

                // We could just yield return nested IEnumerator's here but we choose to do
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
    }
}
