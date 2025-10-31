// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Utilities.Async
{
    internal sealed class YieldInstructionTaskSource<T> : IValueTaskSource<T>
#if UNITY_EDITOR
        , IEditorCancelable
#endif
    {
        private static readonly ConcurrentQueue<YieldInstructionTaskSource<T>> pool = new();

        private readonly YieldInstructionWorker<T> instructionWorker;
        private ManualResetValueTaskSourceCore<T> core;

        internal short Version => core.Version;

        internal bool IsCompleted
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

#if UNITY_EDITOR
        private bool editorCancellationTriggered;
        private EditorPlayModeCancellation.Registration? editorCancellationRegistration;
#endif

        private YieldInstructionTaskSource()
            => instructionWorker = new YieldInstructionWorker<T>(this);

        ~YieldInstructionTaskSource()
            => instructionWorker.Dispose();

        public static YieldInstructionTaskSource<T> Rent(object instruction)
        {
            if (instruction == null)
            {
                throw new ArgumentNullException(nameof(instruction));
            }

            if (!pool.TryDequeue(out var work))
            {
                work = new YieldInstructionTaskSource<T>();
            }

            work.core.Reset();
            work.instructionWorker.Initialize(instruction);
#if UNITY_EDITOR
            try
            {
                work.editorCancellationRegistration?.Dispose();
                work.editorCancellationTriggered = false;
                work.editorCancellationRegistration = EditorPlayModeCancellation.Register(work);
            }
            catch (InvalidOperationException)
            {
                work.instructionWorker.Reset();
                work.core.SetException(new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage));
            }
#endif
            return work;
        }

        public static void Return(YieldInstructionTaskSource<T> taskSource)
        {
            taskSource.instructionWorker.Reset();
#if UNITY_EDITOR
            taskSource.editorCancellationRegistration?.Dispose();
            taskSource.editorCancellationRegistration = null;
            taskSource.editorCancellationTriggered = false;
#endif
            pool.Enqueue(taskSource);
        }

        public void CompleteWork(object taskResult)
        {
            if (IsCompleted) { return; }

            try
            {
                var value = taskResult switch
                {
                    T typedResult => typedResult,
                    null when default(T) is null => default,
                    // ReSharper disable once PossibleInvalidCastException
                    _ => (T)taskResult
                };

                core.SetResult(value);
            }
            catch (Exception e)
            {
                core.SetException(e);
            }
        }

        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token)
            => core.GetStatus(token);

        T IValueTaskSource<T>.GetResult(short token)
            => core.GetResult(token);

        void IValueTaskSource<T>.OnCompleted(Action<object> completedContinuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0 && SyncContextUtility.IsMainThread)
            {
                flags &= ~ValueTaskSourceOnCompletedFlags.UseSchedulingContext;
            }

            core.OnCompleted(completedContinuation, state, token, flags);
        }

#if UNITY_EDITOR
        void IEditorCancelable.CancelFromEditor()
        {
            if (editorCancellationTriggered) { return; }
            editorCancellationTriggered = true;
            editorCancellationRegistration?.Dispose();
            editorCancellationRegistration = null;

            if (!IsCompleted)
            {
                instructionWorker.Reset();
                core.SetException(new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage));
            }
        }
#endif
    }
}
