// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Utilities.Async
{
    internal sealed class BackgroundWork : IValueTaskSource
#if UNITY_EDITOR
        , IEditorCancelable
#endif
    {
        private static readonly ConcurrentQueue<BackgroundWork> pool = new();
        private ManualResetValueTaskSourceCore<object> core;

#if UNITY_EDITOR
        private bool editorCancellationTriggered;
        private EditorPlayModeCancellation.Registration? editorCancellationRegistration;
#endif
        internal short Version => core.Version;

        internal bool IsComplete
        {
            get
            {
                var version = Version;

                if (version == 0)
                {
                    return false;
                }

                return core.GetStatus(version) != ValueTaskSourceStatus.Pending;
            }
        }

        private BackgroundWork() { }

        public static BackgroundWork Rent()
        {
            if (!pool.TryDequeue(out var work))
            {
                work = new BackgroundWork();
            }

            work.core.Reset();
            // Queue the background work which will complete the ValueTask by
            // calling SetResult/SetException on the ManualResetValueTaskSourceCore.
            // This avoids allocating a Task just to represent the background work.
            ThreadPool.UnsafeQueueUserWorkItem(workCallback, work);
            return work;
        }

        public static void Return(BackgroundWork work)
        {
#if UNITY_EDITOR
            work.editorCancellationRegistration?.Dispose();
            work.editorCancellationRegistration = null;
            work.editorCancellationTriggered = false;
#endif
            pool.Enqueue(work);
        }

        void IValueTaskSource.GetResult(short token)
            => core.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
            => core.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object> completedContinuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
#if UNITY_WEBGL
            // On WebGL we can't reliably run background threads, so register
            // the continuation directly and accept it may run on the main
            // thread when the source completes.
            UnityEngine.Debug.LogWarning($"{nameof(BackgroundAwaiter)} not supported for {nameof(UnityEngine.RuntimePlatform.WebGLPlayer)}. Continued on MainThread");
            core.OnCompleted(completedContinuation, state, token, flags);
#else
            // We want the continuation to run on the thread that completes
            // the ValueTask (the thread pool thread). If the awaiting code
            // captured a scheduling context (i.e. awaited without
            // ConfigureAwait(false)), the runtime may wrap the provided
            // continuation so it gets posted back to that context. Clear
            // the UseSchedulingContext flag to indicate we will invoke the
            // continuation on our own (background) thread.
            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                flags &= ~ValueTaskSourceOnCompletedFlags.UseSchedulingContext;
            }

            // Capture the execution context to flow it to the continuation
            // invoked on the thread pool thread.
            var execContext = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0
                ? ExecutionContext.Capture()
                : null;

            var contPayload = ContinuationPayload.Rent(completedContinuation, state, execContext);
            core.OnCompleted(continuationInvokerAction, contPayload, token, flags);
#endif
        }

        private static Action<object> continuationInvokerAction = ContinuationInvoker;

        private static void ContinuationInvoker(object payload)
        {
            if (payload is not ContinuationPayload cont) { return; }

            try
            {
                if (cont.ExecutionContext != null)
                {
                    var exPayload = ContinuationPayload.Rent(cont.Action, cont.State);
                    ExecutionContext.Run(cont.ExecutionContext, contextCallback, exPayload);
                }
                else
                {
                    cont.Action.Invoke(cont.State);
                }
            }
            finally
            {
                ContinuationPayload.Return(cont);
            }
        }

        private static ContextCallback contextCallback = ContextCallbackInvoker;

        private static void ContextCallbackInvoker(object state)
        {
            if (state is not ContinuationPayload cont) { return; }

            try
            {
                cont.Action.Invoke(cont.State);
            }
            finally
            {
                ContinuationPayload.Return(cont);
            }
        }

        private static WaitCallback workCallback = RunWork;

        private static void RunWork(object state)
        {
            if (state is not BackgroundWork work) { return; }

            try
            {
                // Here would be the actual work executed in background. For
                // this awaiter we just complete the ValueTask to resume the
                // awaiting code on a thread pool thread.
                work.core.SetResult(null);
            }
            catch (Exception e)
            {
                work.core.SetException(e);
            }
        }


#if UNITY_EDITOR
        void IEditorCancelable.CancelFromEditor()
        {
            if (editorCancellationTriggered) { return; }
            editorCancellationTriggered = true;
            editorCancellationRegistration?.Dispose();
            editorCancellationRegistration = null;

            if (!IsComplete)
            {
                core.SetException(new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage));
            }
        }
#endif
    }
}
