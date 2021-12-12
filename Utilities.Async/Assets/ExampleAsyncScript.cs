using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async;

public class ExampleAsyncScript : MonoBehaviour
{
    private async void Start()
    {
        try
        {
            // always encapsulate try/catch around
            // async methods called from unity events
            await MyFunctionAsync();

            // switch to background thread to do a long
            // running process on background thread
            await Awaiters.BackgroundThread;

            // Get back to the main unity thread
            await Awaiters.UnityMainThread;

            // await on IEnumerator functions as well
            // for backwards compatibility or older code
            await MyEnumerableFunction();
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
        yield return new WaitForSeconds(1);
    }
}
