// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

#if UNITY_ADDRESSABLES
using UnityEngine.ResourceManagement.AsyncOperations;
using Utilities.Async.Addressables;
#endif

namespace Utilities.Async
{
    internal sealed class CoroutineTaskSource<T> : IValueTaskSource<T>
#if UNITY_EDITOR
        , IEditorCancelable
#endif
    {
        private static readonly ConcurrentQueue<CoroutineTaskSource<T>> pool = new();

        private readonly Action runner;
        private readonly CoroutineWrapper<T> coroutineWrapper;
        private ManualResetValueTaskSourceCore<T> core;

#if UNITY_EDITOR
        private IDisposable editorCancellationRegistration;
        private bool editorCancellationTriggered;
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

        private CoroutineTaskSource()
        {
            coroutineWrapper = new CoroutineWrapper<T>(this);
            runner = () => AwaiterExtensions.RunCoroutine(coroutineWrapper);
        }

        public static CoroutineTaskSource<T> Rent(IEnumerator instruction)
        {
            if (instruction == null)
            {
                throw new ArgumentNullException(nameof(instruction));
            }

            if (!pool.TryDequeue(out var work))
            {
                work = new CoroutineTaskSource<T>();
            }

            work.core.Reset();
            work.coroutineWrapper.Initialize(instruction);
#if UNITY_EDITOR
            try
            {
                work.editorCancellationRegistration?.Dispose();
                work.editorCancellationTriggered = false;
                work.editorCancellationRegistration = EditorPlayModeCancellation.Register(work);
            }
            catch (InvalidOperationException)
            {
                work.coroutineWrapper.Cancel();
                work.core.SetException(new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage));
            }
#endif

            SyncContextUtility.RunOnUnityThread(work.runner);
            return work;
        }

        public static void Return(CoroutineTaskSource<T> taskSource)
        {
            taskSource.coroutineWrapper.Cancel();
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
                T value;

                switch (taskResult)
                {
#if UNITY_ADDRESSABLES
                    case AsyncOperationHandle operationHandle:
                        if (operationHandle.IsValid())
                        {
                            value = (T)operationHandle.Result;
                        }

                        operationHandle.TryThrowException();
                        break;
                    case AsyncOperationHandle<T> operationHandle:
                        if (operationHandle.IsValid())
                        {
                            value = operationHandle.Result;
                        }

                        operationHandle.TryThrowException();
                        break;
#endif // UNITY_ADDRESSABLES
                    case T typedResult:
                        value = typedResult;
                        break;
                    case null when default(T) is null:
                        value = default;
                        break;
                    default:
                        // ReSharper disable once PossibleInvalidCastException
                        value = (T)taskResult;
                        break;
                }

                core.SetResult(value);
            }
            catch (Exception e)
            {
                core.SetException(e);
            }
        }

        internal void FailWithException(Exception e)
        {
            if (Version != 0 && core.GetStatus(Version) != ValueTaskSourceStatus.Pending)
            {
                return;
            }

            core.SetException(e);
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
                coroutineWrapper.Cancel();
                core.SetException(new TaskCanceledException(EditorPlayModeCancellation.CancellationMessage));
            }
        }
#endif
    }
}
