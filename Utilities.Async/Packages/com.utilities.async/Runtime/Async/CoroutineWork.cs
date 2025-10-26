// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_ADDRESSABLES
using UnityEngine.ResourceManagement.AsyncOperations;
using Utilities.Async.Addressables;
#endif

namespace Utilities.Async
{
    internal sealed class CoroutineWork
    {
        private static readonly ConcurrentQueue<CoroutineWork> pool = new();
        private readonly InstructionWrapper instructionWrapper = new();
        private readonly Stack<IEnumerator> processStack = new();
        private readonly ContinuationDispatcher continuationDispatcher = new();

        private TaskCompletionSource<bool> completionSource;
        private bool isActive;
        private bool isCompleted;

        private CoroutineWork() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CoroutineWork Rent(object instruction)
        {
            if (!pool.TryDequeue(out var work))
            {
                work = new CoroutineWork();
                void StartWorkCoroutineRunner() => AwaiterExtensions.RunCoroutine(work.Run());
                SyncContextUtility.RunOnUnityThread(StartWorkCoroutineRunner);
            }

            work.Begin(instruction);
            return work;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(CoroutineWork work)
        {
            work.Reset();
            pool.Enqueue(work);
        }

        internal Task Task
        {
            get
            {
                if (completionSource == null)
                {
                    throw new InvalidOperationException("CoroutineWork has not been rented.");
                }

                return completionSource.Task;
            }
        }

        internal void RegisterContinuation(Action continuation)
        {
            if (continuation == null) { return; }
            continuationDispatcher.Set(continuation);
            var awaiter = Task.GetAwaiter();

            if (awaiter.IsCompleted)
            {
                continuationDispatcher.Invoke();
            }
            else
            {
                awaiter.UnsafeOnCompleted(continuationDispatcher.InvokeAction);
            }
        }

        private void Begin(object instruction)
        {
            isActive = true;
            isCompleted = false;
            completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.None);

            if (instruction is IEnumerator enumerator)
            {
                processStack.Push(enumerator);
            }
            else
            {
                instructionWrapper.Initialize(instruction);
                processStack.Push(instructionWrapper);
            }
        }

        private void Reset()
        {
            completionSource = null;
            isActive = false;
            isCompleted = false;
            processStack.Clear();
            instructionWrapper.Clear();
            continuationDispatcher.Clear();
        }

        private IEnumerator Run()
        {
            while (true)
            {
                if (isCompleted)
                {
                    yield return null;
                    continue;
                }

                if (!isActive)
                {
                    yield return null;
                    continue;
                }

                if (processStack.Count == 0)
                {
                    CompleteSuccessfully();
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
                    var wrapped = processStack.GenerateExceptionTrace(e);
                    CompleteWithException(wrapped);
                    continue;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        CompleteSuccessfully();
                        continue;
                    }
                }

                if (topWorker.Current is IEnumerator item)
                {
                    processStack.Push(item);
                }
                else
                {
                    yield return topWorker.Current;
                }
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private void CompleteSuccessfully()
        {
            isCompleted = true;
            isActive = false;

            try
            {
                completionSource?.TrySetResult(true);
            }
            catch (Exception e)
            {
                completionSource?.TrySetException(e);
            }
        }

        private void CompleteWithException(Exception exception)
        {
            isCompleted = true;
            isActive = false;
            completionSource?.TrySetException(exception);
        }
    }

    internal sealed class CoroutineWork<T>
    {
        private static readonly ConcurrentQueue<CoroutineWork<T>> pool = new();
        private readonly InstructionWrapper instructionWrapper = new();
        private readonly Stack<IEnumerator> processStack = new();
        private readonly ContinuationDispatcher continuationDispatcher = new();

        private TaskCompletionSource<T> completionSource;
        private bool isActive;
        private bool isCompleted;
        private object instruction;

        private CoroutineWork() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CoroutineWork<T> Rent(object instruction)
        {
            if (!pool.TryDequeue(out var work))
            {
                work = new CoroutineWork<T>();
                void StartWorkCoroutineRunner() => AwaiterExtensions.RunCoroutine(work.Run());
                SyncContextUtility.RunOnUnityThread(StartWorkCoroutineRunner);
            }

            work.Begin(instruction);
            return work;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(CoroutineWork<T> work)
        {
            work.Reset();
            pool.Enqueue(work);
        }

        internal Task<T> Task
        {
            get
            {
                if (completionSource == null)
                {
                    throw new InvalidOperationException("CoroutineWork has not been rented.");
                }

                return completionSource.Task;
            }
        }

        internal void RegisterContinuation(Action continuation)
        {
            if (continuation == null) { return; }

            continuationDispatcher.Set(continuation);
            var awaiter = Task.GetAwaiter();

            if (awaiter.IsCompleted)
            {
                continuationDispatcher.Invoke();
            }
            else
            {
                awaiter.UnsafeOnCompleted(continuationDispatcher.InvokeAction);
            }
        }

        private void Begin(object newInstruction)
        {
            instruction = newInstruction;
            isActive = true;
            isCompleted = false;
            completionSource = new TaskCompletionSource<T>(TaskCreationOptions.None);

            if (newInstruction is IEnumerator enumerator)
            {
                processStack.Push(enumerator);
            }
            else
            {
                instructionWrapper.Initialize(newInstruction);
                processStack.Push(instructionWrapper);
            }
        }

        private void Reset()
        {
            completionSource = null;
            instruction = default;
            isActive = false;
            isCompleted = false;
            processStack.Clear();
            instructionWrapper.Clear();
            continuationDispatcher.Clear();
        }

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
                if (isCompleted)
                {
                    yield return null;
                    continue;
                }

                if (!isActive)
                {
                    yield return null;
                    continue;
                }

                if (processStack.Count == 0)
                {
                    CompleteSuccessfully(default);
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
                    var wrapped = processStack.GenerateExceptionTrace(e);
                    CompleteWithException(wrapped);
                    continue;
                }

                if (isDone)
                {
                    processStack.Pop();

                    if (processStack.Count == 0)
                    {
                        object resultValue = null;

                        try
                        {
                            resultValue = ExtractResult(topWorker, currentWorker);
                        }
                        catch (Exception extractionException)
                        {
                            Debug.LogException(extractionException);
                        }

                        CompleteSuccessfully(resultValue);
                        continue;
                    }
                }

                if (topWorker.Current is IEnumerator item)
                {
                    processStack.Push(item);
                }
                else
                {
                    yield return topWorker.Current;
                }
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private object ExtractResult(IEnumerator topWorker, object currentWorker)
        {
            switch (instruction)
            {
#if UNITY_ASSET_BUNDLES
                case AssetBundleCreateRequest assetBundleCreateRequest:
                    return assetBundleCreateRequest.assetBundle;
                case AssetBundleRequest assetBundleRequest:
                    return assetBundleRequest.asset;
#endif // UNITY_ASSET_BUNDLES
                case ResourceRequest resourceRequest:
                    return resourceRequest.asset;
#if !UNITY_2023_1_OR_NEWER
                case AsyncOperation asyncOperation:
                    return asyncOperation;
#endif
                default:
                    return currentWorker ?? topWorker.Current;
            }
        }

        private void CompleteSuccessfully(object resultValue)
        {
            isCompleted = true;
            isActive = false;
            instruction = default;

            try
            {
                completionSource?.TrySetResult((T)resultValue);
            }
            catch (Exception e)
            {
                completionSource?.TrySetException(e);
            }
        }

        private void CompleteWithException(Exception exception)
        {
            isCompleted = true;
            isActive = false;
            instruction = default;
            completionSource?.TrySetException(exception);
        }
    }
}
