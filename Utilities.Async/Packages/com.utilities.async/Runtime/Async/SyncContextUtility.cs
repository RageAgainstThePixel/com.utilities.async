// MIT License

// Copyright(c) 2016 Modest Tree Media Inc

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace Utilities.Async
{
    /// <summary>
    /// Utility class to assist in thread and context synchronization.
    /// </summary>
    public static class SyncContextUtility
    {
#if UNITY_EDITOR
        private const string EXEC = "Exec";

        private static System.Reflection.MethodInfo executionMethod;

        /// <summary>
        /// HACK: makes Unity Editor execute continuations in edit mode.
        /// </summary>
        private static void ExecuteContinuations()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();

            if (executionMethod == null)
            {
                executionMethod = SynchronizationContext.Current.GetType().GetMethod(EXEC, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }

            executionMethod?.Invoke(SynchronizationContext.Current, null);
        }

        static SyncContextUtility() => Initialize();

        [UnityEditor.InitializeOnLoadMethod]
#endif // UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            UnitySynchronizationContext = SynchronizationContext.Current;
            UnityThreadId = Thread.CurrentThread.ManagedThreadId;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += ExecuteContinuations;
#endif // UNITY_EDITOR
        }

        /// <summary>
        /// This Unity Player's Thread ID.
        /// </summary>
        public static int UnityThreadId { get; private set; }

        /// <summary>
        /// This Unity Player's Synchronization Context.
        /// </summary>
        public static SynchronizationContext UnitySynchronizationContext { get; private set; }

        /// <summary>
        /// Is this being called from the main thread?
        /// </summary>
        public static bool IsMainThread
            => UnityThreadId == Thread.CurrentThread.ManagedThreadId;

        private static readonly SendOrPostCallback postCallback = SendOrPostCallback;

        private static readonly ConcurrentQueue<object> actionQueue = new();

        private static void SendOrPostCallback(object @null)
        {
            if (!IsMainThread)
            {
                Debug.LogError($"{nameof(SendOrPostCallback)}::Failed to post on main thread!");
                return;
            }

            try
            {
                while (actionQueue.TryDequeue(out var state))
                {
                    if (state is ActionPayload payload)
                    {
                        try
                        {
                            payload.Action.Invoke();
                        }
                        finally
                        {
                            ActionPayload.Return(payload);
                        }
                    }
                    else if (state is Action action)
                    {
                        action.Invoke();
                    }
                    else
                    {
                        Debug.LogError($"{nameof(SendOrPostCallback)}::state is not an {nameof(Action)} or {nameof(ActionPayload)}!");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void RunOnUnityThread(Action callback)
        {
            try
            {
                if (IsMainThread)
                {
                    callback.Invoke();
                }
                else
                {
                    actionQueue.Enqueue(ActionPayload.Rent(callback));
                    UnitySynchronizationContext.Post(postCallback, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static readonly SendOrPostCallback continuationCallback = ContinuationCallback;

        private static readonly ConcurrentQueue<object> continuationQueue = new();

        private static void ContinuationCallback(object @null)
        {
            if (!IsMainThread)
            {
                Debug.LogError($"{nameof(SendOrPostCallback)}::Failed to post on main thread!");
                return;
            }

            try
            {
                while (continuationQueue.Count > 0)
                {
                    if (continuationQueue.TryPeek(out _) &&
                       continuationQueue.TryDequeue(out var payload))
                    {
                        if (payload is ContinuationPayload continuation)
                        {
                            try
                            {
                                continuation.Action(continuation.State);
                            }
                            finally
                            {
                                ContinuationPayload.Return(continuation);
                            }
                        }
                        else
                        {
                            Debug.LogError($"{nameof(ContinuationCallback)}::payload is not a {nameof(ContinuationPayload)}!");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static void ScheduleContinuation(Action<object> continuation, object state)
        {
            try
            {
                if (IsMainThread)
                {
                    continuation(state);
                }
                else
                {
                    continuationQueue.Enqueue(ContinuationPayload.Rent(continuation, state));
                    UnitySynchronizationContext.Post(continuationCallback, null);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
