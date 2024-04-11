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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
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

        /// <summary>
        /// Runs <see cref="Task"/> with <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="task">The <see cref="Task"/> to run.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
        /// <exception cref="OperationCanceledException"></exception>
        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using (cancellationToken.Register(state => ((TaskCompletionSource<object>)state).TrySetResult(null), tcs))
            {
                var resultTask = await Task.WhenAny(task, tcs.Task);

                if (resultTask == tcs.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                await task;
            }
        }

        /// <summary>
        /// Runs <see cref="Task{T}"/> with <see cref="CancellationToken"/>.
        /// </summary>
        /// <typeparam name="T">Task return type.</typeparam>
        /// <param name="task">The <see cref="Task{T}"/> to run.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns><see cref="Task{T}"/> result.</returns>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using (cancellationToken.Register(state => ((TaskCompletionSource<object>)state).TrySetResult(null), tcs))
            {
                var resultTask = await Task.WhenAny(task, tcs.Task);

                if (resultTask == tcs.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return await task;
            }
        }

        public static SimpleCoroutineAwaiter GetAwaiter(this UnityMainThread instruction)
            => GetAwaiterReturnVoid(instruction);

#if UNITY_WEBGL && !UNITY_EDITOR
        public static SimpleCoroutineAwaiter GetAwaiter(this BackgroundThread instruction)
        {
            Debug.LogWarning($"{nameof(BackgroundThread)} not supported for {nameof(RuntimePlatform.WebGLPlayer)}");
            return GetAwaiterReturnVoid(instruction);
        }
#else
        public static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter(this BackgroundThread _)
            => BackgroundThread.GetAwaiter();
#endif

        public static SimpleCoroutineAwaiter GetAwaiter(this WaitForSeconds instruction)
            => GetAwaiterReturnVoid(instruction);

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
        {
            var awaiter = new SimpleCoroutineAwaiter<AsyncOperation>();
            RunOnUnityScheduler(() => RunCoroutine(ReturnAsyncOperation(awaiter, instruction)));
            return awaiter;
        }

#endif // !UNITY_2023_1_OR_NEWER

        public static SimpleCoroutineAwaiter<Object> GetAwaiter(this ResourceRequest instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<Object>();
            RunOnUnityScheduler(() => RunCoroutine(ResourceRequest(awaiter, instruction)));
            return awaiter;
        }

#if UNITY_ASSET_BUNDLES

        public static SimpleCoroutineAwaiter<AssetBundle> GetAwaiter(this AssetBundleCreateRequest instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<AssetBundle>();
            RunOnUnityScheduler(() => RunCoroutine(AssetBundleCreateRequest(awaiter, instruction)));
            return awaiter;
        }

        public static SimpleCoroutineAwaiter<Object> GetAwaiter(this AssetBundleRequest instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter<Object>();
            RunOnUnityScheduler(() => RunCoroutine(AssetBundleRequest(awaiter, instruction)));
            return awaiter;
        }

#endif //UNITY_ASSET_BUNDLES

        public static SimpleCoroutineAwaiter<T> GetAwaiter<T>(this IEnumerator<T> coroutine)
        {
            var awaiter = new SimpleCoroutineAwaiter<T>();
            RunOnUnityScheduler(() => RunCoroutine(new CoroutineWrapper<T>(coroutine, awaiter).Run()));
            return awaiter;
        }

        public static SimpleCoroutineAwaiter<object> GetAwaiter(this IEnumerator coroutine)
        {
            var awaiter = new SimpleCoroutineAwaiter<object>();
            RunOnUnityScheduler(() => RunCoroutine(new CoroutineWrapper<object>(coroutine, awaiter).Run()));
            return awaiter;
        }

        internal static SimpleCoroutineAwaiter GetAwaiterReturnVoid(object instruction)
        {
            var awaiter = new SimpleCoroutineAwaiter();
            RunOnUnityScheduler(() => RunCoroutine(ReturnVoid(awaiter, instruction)));
            return awaiter;
        }

        [Preserve]
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

        [Preserve]
        private static MonoBehaviour coroutineRunner;

        private static readonly ConcurrentQueue<Action> actionQueue = new ConcurrentQueue<Action>();

        [Preserve]
        internal static void RunOnUnityScheduler(Action action)
        {
            if (SyncContextUtility.IsMainThread)
            {
                action();
            }
            else
            {
                actionQueue.Enqueue(action);
                SyncContextUtility.UnitySynchronizationContext.Post(DeferredPostCallback, null);
            }
        }

        private static void DeferredPostCallback(object state)
        {
            if (!SyncContextUtility.IsMainThread)
            {
                Debug.LogError("Failed to post deferred execution back on main thread!");
                return;
            }

            while (actionQueue.Count > 0)
            {
                if (actionQueue.TryPeek(out _) &&
                    actionQueue.TryDequeue(out var action))
                {
                    action?.Invoke();
                }
            }

            if (actionQueue.Count > 0)
            {
                Debug.LogError("Failed to execute all queued actions!");
            }
        }

        [Preserve]
        private class CoroutineRunner : MonoBehaviour
        {
#if UNITY_WEBGL
            private Func<int> timerSchedulerLoop;

            [Preserve]
            private void Awake()
            {
                var timer = typeof(System.Threading.Timer);
                var scheduler = timer.GetNestedType("Scheduler", System.Reflection.BindingFlags.NonPublic);

                var timerSchedulerInstance = scheduler.GetProperty("Instance")?.GetValue(null);
                timerSchedulerLoop = (Func<int>)scheduler
                    .GetMethod("RunSchedulerLoop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                    .CreateDelegate(typeof(Func<int>), timerSchedulerInstance);
            }

            [Preserve]
            private void Start()
            {
                StartCoroutine(TimerUpdateCoroutine());
            }

            [Preserve]
            private IEnumerator TimerUpdateCoroutine()
            {
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    yield break;
                }
#endif
                while (true)
                {
                    var delay = timerSchedulerLoop();

                    if (delay == -1)
                    {
                        yield return null;
                    }
                    else
                    {
                        yield return new WaitForSeconds(delay / 1000f);
                    }
                }
            }
#endif // UNITY_WEBGL
        }

        private static IEnumerator ReturnVoid(SimpleCoroutineAwaiter awaiter, object instruction)
        {
            // For simple instructions we assume that they don't throw exceptions
            yield return instruction;
            awaiter.Complete();
        }

        private static IEnumerator ResourceRequest(SimpleCoroutineAwaiter<Object> awaiter, ResourceRequest instruction)
        {
            yield return instruction;
            awaiter.Complete(instruction.asset);
        }

#if !UNITY_2023_1_OR_NEWER

        private static IEnumerator ReturnAsyncOperation(SimpleCoroutineAwaiter<AsyncOperation> awaiter, AsyncOperation instruction)
        {
            yield return instruction;
            awaiter.Complete(instruction);
        }

#endif // !UNITY_2023_1_OR_NEWER
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
