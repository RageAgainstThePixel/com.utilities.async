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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async.AwaitYieldInstructions;
using Utilities.Async.Internal;
using Object = UnityEngine.Object;

namespace Utilities.Async
{
    /// <summary>
    /// We could just add a generic GetAwaiter to YieldInstruction and CustomYieldInstruction
    /// but instead we add specific methods to each derived class to allow for return values
    /// that make the most sense for the specific instruction type.
    /// </summary>
    public static class AwaiterExtensions
    {
        /// <summary>
        /// Runs the <see cref="Task"/> as <see cref="IEnumerator"/>.
        /// </summary>
        /// <param name="task">The <see cref="Task"/> to run.</param>
        public static IEnumerator RunAsIEnumerator(this Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    throw task.Exception;
                }
            }
        }

        /// <summary>
        /// Runs the <see cref="Task{T}"/> as <see cref="IEnumerator{T}"/>.
        /// </summary>
        /// <param name="task">The <see cref="Task{T}"/> to run.</param>
        public static IEnumerator<T> RunAsIEnumerator<T>(this Task<T> task)
        {
            while (!task.IsCompleted)
            {
                yield return default;
            }

            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    throw task.Exception;
                }
            }
            else
            {
                yield return task.Result;
            }
        }

        /// <summary>
        /// Runs the <see cref="Func{TResult}"/> as <see cref="IEnumerator"/>.
        /// </summary>
        /// <param name="asyncFunc"><see cref="Func{TResult}"/> to run.</param>
        public static IEnumerator RunAsIEnumerator(Func<Task> asyncFunc)
            => asyncFunc.Invoke().RunAsIEnumerator();

        /// <summary>
        /// Runs the <see cref="Func{TResult}"/> as <see cref="IEnumerator{T}"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asyncFunc"><see cref="Func{TResult}"/> to run.</param>
        public static IEnumerator<T> RunAsIEnumerator<T>(Func<Task<T>> asyncFunc)
            => asyncFunc.Invoke().RunAsIEnumerator();

        /// <summary>
        /// Runs the async task synchronously.
        /// </summary>
        /// <param name="asyncFunc"><see cref="Func{TResult}"/> callback.</param>
        public static void RunSynchronously(Func<Task> asyncFunc)
            => asyncFunc.Invoke().Wait();

        /// <summary>
        /// Runs the async task synchronously.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="asyncFunc"><see cref="Func{TResult}"/> callback.</param>
        public static T RunSynchronously<T>(Func<Task<T>> asyncFunc)
            => asyncFunc.Invoke().Result;

        public static SimpleCoroutineAwaiter GetAwaiter(this WaitForSeconds instruction)
            => GetAwaiterReturnVoid(instruction);

        public static SimpleCoroutineAwaiter GetAwaiter(this UnityMainThread instruction)
            => GetAwaiterReturnVoid(instruction);

        public static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter(this BackgroundThread _)
            => BackgroundThread.GetAwaiter();

        public static SimpleCoroutineAwaiter GetAwaiter(this WaitForEndOfFrame instruction)
            => GetAwaiterReturnVoid(instruction);

        public static SimpleCoroutineAwaiter GetAwaiter(this WaitForFixedUpdate instruction)
            => GetAwaiterReturnVoid(instruction);

        public static SimpleCoroutineAwaiter GetAwaiter(this WaitForSecondsRealtime instruction)
            => GetAwaiterReturnVoid(instruction);

        public static SimpleCoroutineAwaiter GetAwaiter(this WaitUntil instruction)
            => GetAwaiterReturnVoid(instruction);

        public static SimpleCoroutineAwaiter GetAwaiter(this WaitWhile instruction)
            => GetAwaiterReturnVoid(instruction);

#if !UNITY_2023_1_OR_NEWER
        public static SimpleCoroutineAwaiter<AsyncOperation> GetAwaiter(this AsyncOperation instruction)
            => GetAwaiterReturnSelf(instruction);
#endif

        public static SimpleCoroutineAwaiter<Object> GetAwaiter(this ResourceRequest instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<Object>();
            var enumerator = ResourceRequest(awaiter, instruction);
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

#if UNITY_ASSET_BUNDLES

        public static SimpleCoroutineAwaiter<AssetBundle> GetAwaiter(this AssetBundleCreateRequest instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<AssetBundle>();
            var enumerator = AssetBundleCreateRequest(awaiter, instruction);
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

        public static SimpleCoroutineAwaiter<Object> GetAwaiter(this AssetBundleRequest instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<Object>();
            var enumerator = AssetBundleRequest(awaiter, instruction);
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

#endif //UNITY_ASSET_BUNDLES

        public static SimpleCoroutineAwaiter<T> GetAwaiter<T>(this IEnumerator<T> coroutine)
        {
            var awaiter = new SimpleCoroutineAwaiter<T>();
            var enumerator = new CoroutineWrapper<T>(coroutine, awaiter).Run();
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

        public static SimpleCoroutineAwaiter<object> GetAwaiter(this IEnumerator coroutine)
        {
            var awaiter = new SimpleCoroutineAwaiter<object>();
            var enumerator = new CoroutineWrapper<object>(coroutine, awaiter).Run();
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

        internal static SimpleCoroutineAwaiter GetAwaiterReturnVoid(object instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter();
            var enumerator = ReturnVoid(awaiter, instruction);
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

        internal static SimpleCoroutineAwaiter<T> GetAwaiterReturnSelf<T>(T instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<T>();
            var enumerator = ReturnSelf(awaiter, instruction);
            RunOnUnityScheduler(() => RunCoroutine(enumerator));
            return awaiter;
        }

        internal static void RunCoroutine(IEnumerator enumerator)
        {
            if (Application.isPlaying)
            {
                if (coroutineRunner == null)
                {
                    var go = GameObject.Find(nameof(CoroutineRunner));

                    if (go == null)
                    {
                        go = new GameObject(nameof(CoroutineRunner));
                    }

                    Object.DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    coroutineRunner = go.TryGetComponent<CoroutineRunner>(out var runner) ? runner : go.AddComponent<CoroutineRunner>();
                }

                coroutineRunner.StartCoroutine(enumerator);
            }
            else
            {
#if UNITY_EDITOR
                Unity.EditorCoroutines.Editor.EditorCoroutineUtility.StartCoroutineOwnerless(enumerator);
#else
                throw new Exception(nameof(CoroutineRunner));
#endif
            }
        }

        private static MonoBehaviour coroutineRunner;

        internal static void RunOnUnityScheduler(Action action)
        {
            if (SynchronizationContext.Current == SyncContextUtility.UnitySynchronizationContext)
            {
                action();
            }
            else
            {
                void SendOrPostCallback(object state) => action();
                SyncContextUtility.UnitySynchronizationContext.Post(SendOrPostCallback, null);
            }
        }

        private class CoroutineRunner : MonoBehaviour { }
        private static IEnumerator ReturnVoid(SimpleCoroutineAwaiter awaiter, object instruction)
        {
            // For simple instructions we assume that they don't throw exceptions
            yield return instruction;
            awaiter.Complete();
        }

        private static IEnumerator ReturnSelf<T>(SimpleCoroutineAwaiter<T> awaiter, T instruction)
        {
            yield return instruction;
            awaiter.Complete(instruction);
        }

        private static IEnumerator ResourceRequest(SimpleCoroutineAwaiter<Object> awaiter, ResourceRequest instruction)
        {
            yield return instruction;
            awaiter.Complete(instruction.asset);
        }

#if UNITY_ASSET_BUNDLES

        private static IEnumerator AssetBundleCreateRequest(SimpleCoroutineAwaiter<AssetBundle> awaiter, AssetBundleCreateRequest instruction)
        {
            yield return instruction;
            awaiter.Complete(instruction.assetBundle);
        }

        private static IEnumerator AssetBundleRequest(SimpleCoroutineAwaiter<Object> awaiter, AssetBundleRequest instruction)
        {
            yield return instruction;
            awaiter.Complete(instruction.asset);
        }

#endif // UNITY_ASSET_BUNDLES

    }
}
