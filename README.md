# com.utilities.async

[![openupm](https://img.shields.io/npm/v/com.utilities.async?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.utilities.async/)

A Utilities.Async package for the [Unity](https://unity.com/) Game Engine.

Adapted from https://github.com/svermeulen/Unity3dAsyncAwaitUtil

For details on usage see the associated blog post [here](http://www.stevevermeulen.com/index.php/2017/09/23/using-async-await-in-unity3d-2017/).

## Installing

### Via Unity Package Manager and OpenUPM

- Open your Unity project settings
- Select the `Package Manager`
![scoped-registries](Documentation~/images/package-manager-scopes.png)
- Add the OpenUPM package registry:
  - `Name: OpenUPM`
  - `URL: https://package.openupm.com`
  - `Scope(s):`
    - `com.utilities`
- Open the Unity Package Manager window
- Change the Registry from Unity to `My Registries`
- Add the `Utilities.Async` package

### Via Unity Package Manager and Git url

- Open your Unity Package Manager
- Add package from git url: `https://github.com/RageAgainstThePixel/com.utilities.async.git#upm`

## Getting Started

### How to use

```csharp
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
```
