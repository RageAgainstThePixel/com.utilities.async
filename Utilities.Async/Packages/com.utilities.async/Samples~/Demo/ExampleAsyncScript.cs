// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async.AwaitYieldInstructions;
using Utilities.Async.Internal;
using Debug = UnityEngine.Debug;

namespace Utilities.Async.Samples.Demo
{
    public class ExampleAsyncScript : MonoBehaviour
    {
        private async void Start()
        {
            try
            {
                Debug.Log($"{nameof(ExampleAsyncScript)} starting...");
                var stopwatch = Stopwatch.StartNew();

                // Make sure we're on the main unity thread
                await Awaiters.UnityMainThread;
                Debug.Log($"{nameof(UnityMainThread)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

                // Wait for one second using built in coroutine yield
                await new WaitForSeconds(1f);
                Debug.Log($"{nameof(WaitForSeconds)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

                // always encapsulate try/catch around
                // async methods called from unity events
                // this is a long running task that returns to main thread
                await MyFunctionAsync().ConfigureAwait(true);
                Debug.Log($"{nameof(MyFunctionAsync)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

                // A long running task that ends up on a background thread
                await MyFunctionAsync().ConfigureAwait(false);
                Debug.Log($"{nameof(MyFunctionAsync)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

                // Get back to the main unity thread
                await Awaiters.UnityMainThread;
                Debug.Log($"{nameof(UnityMainThread)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

                // switch to background thread to do a long
                // running process on background thread
                // Is not supported for WebGL!!!
                await Awaiters.BackgroundThread;

                Action backgroundInvokedAction = BackgroundInvokedAction;
                backgroundInvokedAction.InvokeOnMainThread();

                // should still be on background thread.
                Debug.Log($"{nameof(BackgroundThread)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

                // await on IEnumerator functions as well
                // for backwards compatibility or older code
                await MyEnumerableFunction();
                Debug.Log($"{nameof(MyEnumerableFunction)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

                await SceneManager.LoadSceneAsync(0)
                    .WithProgress(new Progress<float>(f => Debug.Log($"LoadSceneAsync | {nameof(SyncContextUtility.IsMainThread)} ? {SyncContextUtility.IsMainThread} | {f:P}%")));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            Debug.Log($"{nameof(ExampleAsyncScript)} Complete!");
        }

        private void BackgroundInvokedAction()
        {
            Debug.Log($"{nameof(BackgroundInvokedAction)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread}");
        }

        private async Task MyFunctionAsync()
        {
            await Task.Delay(1000).ConfigureAwait(false);
        }

        private IEnumerator MyEnumerableFunction()
        {
            yield return new WaitForSeconds(1);
            // We can even yield async functions
            // for better interoperability
            yield return MyFunctionAsync();
        }
    }
}
