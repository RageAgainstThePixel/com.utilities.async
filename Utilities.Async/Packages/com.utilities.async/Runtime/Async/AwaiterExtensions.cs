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
using UnityEngine.Scripting;
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
#if UNITY_6000_0_OR_NEWER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task AsTask(this Awaitable awaitable)
            => await awaitable;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async Task<T> AsTask<T>(this Awaitable<T> awaitable)
            => await awaitable;
#endif // UNITY_6000_0_OR_NEWER

        /// <summary>
        /// Runs the <see cref="Task"/> as <see cref="IEnumerator"/>.
        /// </summary>
        /// <param name="task">The <see cref="Task"/> to run.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerator RunAsIEnumerator(Func<Task> asyncFunc)
            => asyncFunc.Invoke().RunAsIEnumerator();

        /// <summary>
        /// Runs the <see cref="Func{TResult}"/> as <see cref="IEnumerator{T}"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asyncFunc"><see cref="Func{TResult}"/> to run.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerator<T> RunAsIEnumerator<T>(Func<Task<T>> asyncFunc)
            => asyncFunc.Invoke().RunAsIEnumerator();

        /// <summary>
        /// Runs the async task synchronously.
        /// </summary>
        /// <param name="asyncFunc"><see cref="Func{TResult}"/> callback.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunSynchronously(Func<Task> asyncFunc)
            => asyncFunc.Invoke().Wait();

        /// <summary>
        /// Runs the async task synchronously.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="asyncFunc"><see cref="Func{TResult}"/> callback.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                var resultTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(true);

                if (resultTask == tcs.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                await task.ConfigureAwait(true);
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
                var resultTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(true);

                if (resultTask == tcs.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return await task.ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Runs <see cref="AsyncOperation"/> with <see cref="IProgress{T}"/>.
        /// </summary>
        /// <param name="operation"><see cref="AsyncOperation"/>.</param>
        /// <param name="progress"><see cref="IProgress{T}"/></param>
        /// <returns><see cref="AsyncOperation"/></returns>
        public static async Task<AsyncOperation> WithProgress(this AsyncOperation operation, IProgress<float> progress)
        {
            if (operation.isDone) { return operation; }
            Thread backgroundThread = null;
            var opTcs = new TaskCompletionSource<AsyncOperation>();
            try
            {
                if (progress != null)
                {
                    backgroundThread = new Thread(() => ProgressThread(operation, progress))
                    {
                        IsBackground = true
                    };

                    async void ProgressThread(AsyncOperation asyncOp, IProgress<float> tProgress)
                    {
                        await Awaiters.UnityMainThread;

                        try
                        {
                            while (!asyncOp.isDone)
                            {
                                tProgress.Report(asyncOp.progress);
                                await Awaiters.UnityMainThread;
                            }
                        }
                        catch (Exception)
                        {
                            // throw away
                        }
                    }
                }

                backgroundThread?.Start();
                operation.completed += OnCompleted;
                return await opTcs.Task.ConfigureAwait(true);
            }
            finally
            {
                operation.completed -= OnCompleted;
                progress?.Report(100f);
                backgroundThread?.Join();
            }

            void OnCompleted(AsyncOperation op) => opTcs.SetResult(op);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this UnityMainThread instruction)
            => new(instruction);

#if UNITY_WEBGL && !UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this BackgroundThread instruction)
        {
            Debug.LogWarning($"{nameof(BackgroundThread)} not supported for {nameof(RuntimePlatform.WebGLPlayer)}");
            return new YieldInstructionAwaiter(instruction);
        }
#else
        public static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter(this BackgroundThread _)
            => BackgroundThread.GetAwaiter();
#endif // UNITY_WEBGL && !UNITY_EDITOR

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this WaitForSeconds instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this WaitForEndOfFrame instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this WaitForFixedUpdate instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this WaitForSecondsRealtime instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this WaitUntil instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this WaitWhile instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter<Object> GetAwaiter(this ResourceRequest instruction)
            => new(instruction);

#if !UNITY_2023_1_OR_NEWER

        public static YieldInstructionAwaiter<AsyncOperation> GetAwaiter(this AsyncOperation instruction)
            => new(instruction);

#endif // !UNITY_2023_1_OR_NEWER
#if UNITY_ASSET_BUNDLES

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter<AssetBundle> GetAwaiter(this AssetBundleCreateRequest instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter<Object> GetAwaiter(this AssetBundleRequest instruction)
            => new(instruction);

#endif //UNITY_ASSET_BUNDLES

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this CustomYieldInstruction instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YieldInstructionAwaiter GetAwaiter(this YieldInstruction instruction)
            => new(instruction);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CoroutineAwaiter<T> GetAwaiter<T>(this IEnumerator<T> coroutine)
            => new(coroutine);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CoroutineAwaiter GetAwaiter(this IEnumerator coroutine)
            => new(coroutine);

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

        [Preserve]
        private class CoroutineRunner : MonoBehaviour
        {
#if UNITY_WEBGL
            private Func<int> timerSchedulerLoop;

            [Preserve]
            private void Awake()
            {
                var timer = typeof(Timer);
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
    }
}
