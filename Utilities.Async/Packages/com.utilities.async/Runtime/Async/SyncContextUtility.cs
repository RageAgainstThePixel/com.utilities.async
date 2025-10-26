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
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

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
        public static bool IsMainThread => UnitySynchronizationContext == SynchronizationContext.Current;

        private static SendOrPostCallback postCallback = SendOrPostCallback;

        private static void SendOrPostCallback(object state)
        {
            if (IsMainThread && state is Action action)
            {
                action.Invoke();
            }
        }

        [Preserve]
        public static void RunOnUnityThread(Action action)
        {
            if (IsMainThread)
            {
                action?.Invoke();
            }
            else
            {
                UnitySynchronizationContext.Post(postCallback, action);
            }
        }
    }
}
