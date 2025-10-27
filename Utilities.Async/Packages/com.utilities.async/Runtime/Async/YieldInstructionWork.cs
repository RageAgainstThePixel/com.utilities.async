// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace Utilities.Async
{
    internal sealed class YieldInstructionWork<T> : IValueTaskSource<T>
    {
        private static readonly ConcurrentQueue<YieldInstructionWork<T>> pool = new();

        private readonly Action runner;
        private readonly YieldInstructionWrapper<T> instructionWrapper;

        private Action<object> continuation;
        private object continuationState;
        private ValueTaskSourceStatus status;
        private Exception exception;
        private T result;

        private YieldInstructionWork()
        {
            instructionWrapper = new YieldInstructionWrapper<T>(this);
            runner = () => AwaiterExtensions.RunCoroutine(instructionWrapper);
            status = ValueTaskSourceStatus.Pending;
            Version = 0;
        }

        internal short Version { get; private set; }

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
            SyncContextUtility.RunOnUnityThread(work.runner);
            return work;
        }

        public static void Return(YieldInstructionWork<T> work)
        {
            work.result = default;
            work.exception = null;
            work.status = ValueTaskSourceStatus.Pending;
            work.continuation = null;
            work.continuationState = null;
            work.instructionWrapper.Clear();
            pool.Enqueue(work);
        }

        public void CompleteWork(object taskResult)
        {
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
            catch (Exception ex)
            {
                exception = ex;
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
            ScheduleContinuation(continuationCopy, stateCopy);
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
                ScheduleContinuation(completedContinuation, state);
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

        private static void ScheduleContinuation(Action<object> action, object state)
        {
            if (SyncContextUtility.IsMainThread)
            {
                action(state);
                return;
            }

            var payload = ContinuationPayload.Rent(action, state);
            SyncContextUtility.UnitySynchronizationContext.Post(ContinuationCallback, payload);
        }

        private static readonly SendOrPostCallback ContinuationCallback = static payloadObj =>
        {
            var payload = (ContinuationPayload)payloadObj;

            try
            {
                payload.Action(payload.State);
            }
            finally
            {
                ContinuationPayload.Return(payload);
            }
        };

        private sealed class ContinuationPayload
        {
            private static readonly ConcurrentQueue<ContinuationPayload> payloadPool = new();

            public Action<object> Action;
            public object State;

            private ContinuationPayload() { }

            public static ContinuationPayload Rent(Action<object> action, object state)
            {
                if (!payloadPool.TryDequeue(out var payload))
                {
                    payload = new ContinuationPayload();
                }

                payload.Action = action;
                payload.State = state;
                return payload;
            }

            public static void Return(ContinuationPayload payload)
            {
                payload.Action = null;
                payload.State = null;
                payloadPool.Enqueue(payload);
            }
        }
    }
}
