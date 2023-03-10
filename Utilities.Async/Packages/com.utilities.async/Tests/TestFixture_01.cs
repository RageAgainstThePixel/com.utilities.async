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
            try
            {
                var stopwatch = Stopwatch.StartNew();

                // always encapsulate try/catch around
                // async methods called from unity events
                // this is a long running task that returns to main thread
                await MyFunctionAsync().ConfigureAwait(true);
                Debug.Log($"{nameof(MyFunctionAsync)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");
                Assert.IsTrue(SyncContextUtility.IsMainThread);

                // A long running task that ends up on a background thread
                await MyFunctionAsync().ConfigureAwait(false);
                Debug.Log($"{nameof(MyFunctionAsync)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");
                Assert.IsFalse(SyncContextUtility.IsMainThread);

                // Get back to the main unity thread
                await Awaiters.UnityMainThread;
                Debug.Log($"{nameof(UnityMainThread)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");
                Assert.IsTrue(SyncContextUtility.IsMainThread);

                // switch to background thread to do a long
                // running process on background thread
                await Awaiters.BackgroundThread;
                Debug.Log($"{nameof(BackgroundThread)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");
                Assert.IsFalse(SyncContextUtility.IsMainThread);

                // await on IEnumerator functions as well
                // for backwards compatibility or older code
                await MyEnumerableFunction();
                Debug.Log($"{nameof(MyEnumerableFunction)} | {nameof(SyncContextUtility.IsMainThread)}? {SyncContextUtility.IsMainThread} | {stopwatch.ElapsedMilliseconds}");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private async Task MyFunctionAsync()
        {
            await Task.Delay(1000);
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
