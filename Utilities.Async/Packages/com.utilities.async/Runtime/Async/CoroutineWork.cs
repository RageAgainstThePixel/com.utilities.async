// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks.Sources;

#if UNITY_ADDRESSABLES
using UnityEngine.ResourceManagement.AsyncOperations;
using Utilities.Async.Addressables;
#endif

namespace Utilities.Async
{
    internal sealed class CoroutineWork<T> : IValueTaskSource<T>
#if UNITY_EDITOR
        , IEditorCancelable
#endif
    {
        private static readonly ConcurrentQueue<CoroutineWork<T>> pool = new();

        private readonly Action runner;
        private readonly CoroutineWrapper<T> coroutineWrapper;

#if UNITY_EDITOR
        private IDisposable editorCancellationRegistration;
        private bool editorCancellationTriggered;
#endif

        private Action<object> continuation;
        private object continuationState;
        private ValueTaskSourceStatus status;
        private Exception exception;
        private T result;

        internal short Version { get; private set; }

        private CoroutineWork()
        {
            coroutineWrapper = new CoroutineWrapper<T>(this);
            runner = () => AwaiterExtensions.RunCoroutine(coroutineWrapper);
            status = ValueTaskSourceStatus.Pending;
            Version = 0;
        }

        public static CoroutineWork<T> Rent(IEnumerator instruction)
        {
            if (instruction == null)
            {
                throw new InvalidOperationException($"{nameof(instruction)} cannot be null!");
            }

            if (!pool.TryDequeue(out var work))
            {
                work = new CoroutineWork<T>();
            }

            work.status = ValueTaskSourceStatus.Pending;
            work.exception = null;
            work.result = default;
            work.continuation = null;
            work.continuationState = null;

            unchecked
            {
                work.Version++;
            }

            if (work.Version == 0)
            {
                work.Version = 1;
            }

            work.coroutineWrapper.Initialize(instruction);
#if UNITY_EDITOR
            work.editorCancellationRegistration?.Dispose();
            work.editorCancellationRegistration = EditorPlayModeCancellation.Register(work);
            work.editorCancellationTriggered = false;
#endif

            SyncContextUtility.RunOnUnityThread(work.runner);
            return work;
        }

        public static void Return(CoroutineWork<T> work)
        {
            work.result = default;
            work.exception = null;
            work.status = ValueTaskSourceStatus.Pending;
            work.continuation = null;
            work.continuationState = null;
            work.coroutineWrapper.Clear();
#if UNITY_EDITOR
            work.editorCancellationRegistration?.Dispose();
            work.editorCancellationRegistration = null;
            work.editorCancellationTriggered = false;
#endif
            pool.Enqueue(work);
        }

        public void CompleteWork(object taskResult)
        {
            if (status != ValueTaskSourceStatus.Pending) { return; }

            try
            {
                switch (taskResult)
                {
#if UNITY_ADDRESSABLES
                    case AsyncOperationHandle operationHandle:
                        if (operationHandle.IsValid())
                        {
                            result = (T)operationHandle.Result;
                        }

                        operationHandle.TryThrowException();
                        break;
                    case AsyncOperationHandle<T> operationHandle:
                        if (operationHandle.IsValid())
                        {
                            result = operationHandle.Result;
                        }

                        operationHandle.TryThrowException();
                        break;
#endif
                    case T typedResult:
                        result = typedResult;
                        break;
                    case null when default(T) is null:
                        result = default;
                        break;
                    default:
                        // ReSharper disable once PossibleInvalidCastException
                        result = (T)taskResult;
                        break;
                }

                status = ValueTaskSourceStatus.Succeeded;
            }
            catch (Exception e)
            {
                exception = e;
                status = ValueTaskSourceStatus.Faulted;
            }

            InvokeContinuation();
        }

        private void InvokeContinuation()
        {
            var continuationCopy = continuation;
            if (continuationCopy == null) { return; }
            var stateCopy = continuationState;
            continuation = null;
            continuationState = null;
            SyncContextUtility.ScheduleContinuation(continuationCopy, stateCopy);
        }

        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token)
        {
            ValidateToken(token);
            return status;
        }

        T IValueTaskSource<T>.GetResult(short token)
        {
            ValidateToken(token);

            if (status == ValueTaskSourceStatus.Pending)
            {
                throw new InvalidOperationException("Operation has not completed yet.");
            }

            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return result;
        }

        void IValueTaskSource<T>.OnCompleted(Action<object> completedContinuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            ValidateToken(token);

            if (completedContinuation == null)
            {
                throw new ArgumentNullException(nameof(completedContinuation));
            }

            if (status != ValueTaskSourceStatus.Pending)
            {
                SyncContextUtility.ScheduleContinuation(completedContinuation, state);
                return;
            }

            continuation = completedContinuation;
            continuationState = state;
        }

        private void ValidateToken(short token)
        {
            if (token != Version)
            {
                throw new InvalidOperationException("Token does not match the current operation version.");
            }
        }

#if UNITY_EDITOR
        void IEditorCancelable.CancelFromEditor()
        {
            if (editorCancellationTriggered) { return; }
            editorCancellationTriggered = true;
            editorCancellationRegistration?.Dispose();
            editorCancellationRegistration = null;

            if (status == ValueTaskSourceStatus.Pending)
            {
                coroutineWrapper.Cancel();
                result = default;
                exception = new OperationCanceledException(EditorPlayModeCancellation.CancellationMessage);
                status = ValueTaskSourceStatus.Canceled;
            }

            InvokeContinuation();
        }
#endif
    }
}
