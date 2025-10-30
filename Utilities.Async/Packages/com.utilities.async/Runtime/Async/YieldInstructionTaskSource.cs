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

        private readonly Action runner;
        private readonly YieldInstructionWrapper<T> instructionWrapper;
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
        private IDisposable editorCancellationRegistration;
        private bool editorCancellationTriggered;
#endif

        private YieldInstructionTaskSource()
        {
            instructionWrapper = new YieldInstructionWrapper<T>(this);
            runner = () => AwaiterExtensions.RunCoroutine(instructionWrapper);
        }

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
            work.instructionWrapper.Initialize(instruction);
#if UNITY_EDITOR
            try
            {
                work.editorCancellationRegistration?.Dispose();
                work.editorCancellationTriggered = false;
                work.editorCancellationRegistration = EditorPlayModeCancellation.Register(work);
            }
            catch (InvalidOperationException)
            {
                work.instructionWrapper.Cancel();
                work.core.SetException(new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage));
            }
#endif

            SyncContextUtility.RunOnUnityThread(work.runner);
            return work;
        }

        public static void Return(YieldInstructionTaskSource<T> taskSource)
        {
            taskSource.instructionWrapper.Cancel();
#if UNITY_EDITOR
            taskSource.editorCancellationRegistration?.Dispose();
            taskSource.editorCancellationRegistration = null;
            taskSource.editorCancellationTriggered = false;
#endif
            pool.Enqueue(taskSource);
        }

        public void CompleteWork(object taskResult)
        {
            if (Version != 0 && core.GetStatus(Version) != ValueTaskSourceStatus.Pending)
            {
                return;
            }

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
            => core.OnCompleted(completedContinuation, state, token, flags);

#if UNITY_EDITOR
        void IEditorCancelable.CancelFromEditor()
        {
            if (editorCancellationTriggered) { return; }
            editorCancellationTriggered = true;
            editorCancellationRegistration?.Dispose();
            editorCancellationRegistration = null;

            if (Version != 0 && core.GetStatus(Version) == ValueTaskSourceStatus.Pending)
            {
                instructionWrapper.Cancel();
                core.SetException(new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage));
            }
        }
#endif
    }
}
