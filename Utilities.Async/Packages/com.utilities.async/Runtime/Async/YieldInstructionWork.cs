// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Utilities.Async
{
    internal sealed class YieldInstructionWork<T> : IValueTaskSource<T>
#if UNITY_EDITOR
        , IEditorCancelable
#endif
    {
        private static readonly ConcurrentQueue<YieldInstructionWork<T>> pool = new();

        private readonly Action runner;
        private readonly YieldInstructionWrapper<T> instructionWrapper;

        private Action<object> continuation;
        private object continuationState;
        private ValueTaskSourceStatus status;
        private Exception exception;
        private T result;

        internal short Version { get; private set; }

#if UNITY_EDITOR
        private IDisposable editorCancellationRegistration;
        private bool editorCancellationTriggered;
#endif

        private YieldInstructionWork()
        {
            instructionWrapper = new YieldInstructionWrapper<T>(this);
            runner = () => AwaiterExtensions.RunCoroutine(instructionWrapper);
            status = ValueTaskSourceStatus.Pending;
            Version = 0;
        }

        public static YieldInstructionWork<T> Rent(object instruction)
        {
            if (instruction == null)
            {
                throw new ArgumentNullException(nameof(instruction));
            }

            if (!pool.TryDequeue(out var work))
            {
                work = new YieldInstructionWork<T>();
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

            work.instructionWrapper.Initialize(instruction);
#if UNITY_EDITOR
            work.editorCancellationRegistration?.Dispose();
            work.editorCancellationTriggered = false;
            work.editorCancellationRegistration = EditorPlayModeCancellation.Register(work);
#endif

            SyncContextUtility.RunOnUnityThread(work.runner);
            return work;
        }

        public static void Return(YieldInstructionWork<T> work)
        {
            work.result = default;
            work.exception = null;
            work.status = ValueTaskSourceStatus.Pending;
            Interlocked.Exchange(ref work.continuation, null);
            Volatile.Write(ref work.continuationState, null);
            work.instructionWrapper.Clear();
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
                result = taskResult switch
                {
                    T typedResult => typedResult,
                    null when default(T) is null => default,
                    // ReSharper disable once PossibleInvalidCastException
                    _ => (T)taskResult
                };

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
            var cont = Interlocked.Exchange(ref continuation, null);
            if (cont == null) { return; }
            var state = Volatile.Read(ref continuationState);
            Volatile.Write(ref continuationState, null);
            SyncContextUtility.ScheduleContinuation(cont, state);
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

            if (status == ValueTaskSourceStatus.Canceled)
            {
                if (exception is TaskCanceledException tce)
                {
                    throw tce;
                }

                throw new TaskCanceledException();
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

            Volatile.Write(ref continuationState, state);
            var prev = Interlocked.CompareExchange(ref continuation, completedContinuation, null);

            if (prev != null)
            {
                SyncContextUtility.ScheduleContinuation(completedContinuation, state);
                return;
            }

            if (status != ValueTaskSourceStatus.Pending)
            {
                var c = Interlocked.Exchange(ref continuation, null);
                var s = Volatile.Read(ref continuationState);
                Volatile.Write(ref continuationState, null);

                if (c != null)
                {
                    SyncContextUtility.ScheduleContinuation(c, s);
                }
            }
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
                instructionWrapper.Cancel();
                result = default;
                exception = new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage);
                status = ValueTaskSourceStatus.Canceled;
            }

            InvokeContinuation();
        }
#endif
    }
}
