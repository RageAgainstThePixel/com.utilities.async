// Licensed under the MIT License. See LICENSE in the project root for license information.

using NUnit.Framework;
using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.EditorCoroutines.Editor;
using UnityEngine.TestTools;
using Utilities.Async.AwaitYieldInstructions;
using Utilities.Async.Internal;
using Debug = UnityEngine.Debug;

namespace Utilities.Async.Tests
{
    public class TestFixture_01
    {
        [UnityTest]
        public IEnumerator Test_01_IEnumerator()
        {
            Func<Task> testTask = Test_02_Async;
            yield return AwaiterExtensions.RunAsIEnumerator(testTask);
        }

        [Test]
        public async Task Test_02_Async()
        {
            Debug.Log($"{nameof(Test_02_Async)} starting...");
            var stopwatch = Stopwatch.StartNew();

            // always encapsulate try/catch around
            // async methods called from unity events
            // this is a long running task that returns to main thread
            await MyFunctionAsync().ConfigureAwait(true);
            var isMainThread = SyncContextUtility.IsMainThread;
            Debug.Log($"{nameof(MyFunctionAsync)} | {nameof(SyncContextUtility.IsMainThread)}? {isMainThread} | {stopwatch.ElapsedMilliseconds}");
            Assert.IsTrue(isMainThread);

            // A long running task that ends up on a background thread
            await MyFunctionAsync().ConfigureAwait(false);
            isMainThread = SyncContextUtility.IsMainThread;
            Debug.Log($"{nameof(MyFunctionAsync)} | {nameof(SyncContextUtility.IsMainThread)}? {isMainThread} | {stopwatch.ElapsedMilliseconds}");
            Assert.IsFalse(isMainThread);

            // Get back to the main unity thread
            await Awaiters.UnityMainThread;
            isMainThread = SyncContextUtility.IsMainThread;
            Debug.Log($"{nameof(UnityMainThread)} | {nameof(SyncContextUtility.IsMainThread)}? {isMainThread} | {stopwatch.ElapsedMilliseconds}");
            Assert.IsTrue(isMainThread);

            // switch to background thread to do a long
            // running process on background thread
            await Awaiters.BackgroundThread;
            isMainThread = SyncContextUtility.IsMainThread;
            Debug.Log($"{nameof(BackgroundThread)} | {nameof(SyncContextUtility.IsMainThread)}? {isMainThread} | {stopwatch.ElapsedMilliseconds}");
            Assert.IsFalse(isMainThread);

            Action backgroundInvokedAction = BackgroundInvokedAction;
            backgroundInvokedAction.InvokeOnMainThread();

            // should still be on background thread.
            isMainThread = SyncContextUtility.IsMainThread;
            Debug.Log($"{nameof(BackgroundThread)} | {nameof(SyncContextUtility.IsMainThread)}? {isMainThread} | {stopwatch.ElapsedMilliseconds}");
            Assert.IsFalse(isMainThread);

            // await on IEnumerator functions as well
            // for backwards compatibility or older code
            await MyEnumerableFunction();
            Debug.Log($"{nameof(MyEnumerableFunction)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");

            Debug.Log($"{nameof(Test_02_Async)} Complete!");
        }

        private void BackgroundInvokedAction()
        {
            var isMainThread = SyncContextUtility.IsMainThread;
            Debug.Log($"{nameof(BackgroundInvokedAction)} | {nameof(SyncContextUtility.IsMainThread)}? {isMainThread}");
            Assert.IsTrue(isMainThread);
        }

        private async Task MyFunctionAsync()
        {
            // similar to Task.Delay(1000)
            await Awaiters.DelayAsync(1000).ConfigureAwait(false);
        }

        private IEnumerator MyEnumerableFunction()
        {
            yield return new EditorWaitForSeconds(1);
            // We can even yield async functions
            // for better interoperability
            yield return MyFunctionAsync();
        }
    }
}
