# com.utilities.async

[![Discord](https://img.shields.io/discord/855294214065487932.svg?label=&logo=discord&logoColor=ffffff&color=7389D8&labelColor=6A7EC2)](https://discord.gg/xQgMW9ufN4) [![openupm](https://img.shields.io/npm/v/com.utilities.async?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.utilities.async/) [![openupm](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&query=%24.downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.utilities.async)](https://openupm.com/packages/com.utilities.async/)

A Utilities.Async package for the [Unity](https://unity.com/) Game Engine.

Adapted from <https://github.com/svermeulen/Unity3dAsyncAwaitUtil>

For details on usage see the [associated blog post here](https://web.archive.org/web/20170926153045/http://www.stevevermeulen.com/index.php/2017/09/23/using-async-await-in-unity3d-2017/).

## Installing

Requires Unity 2021.3 LTS or higher.

The recommended installation method is though the unity package manager and [OpenUPM](https://openupm.com/packages/com.utilities.async).

### Via Unity Package Manager and OpenUPM

#### Terminal

```terminal
openupm add com.utilities.async
```

#### Manual

- Open your Unity project settings
- Select the `Package Manager`
![scoped-registries](images/package-manager-scopes.png)
- Add the OpenUPM package registry:
  - Name: `OpenUPM`
  - URL: `https://package.openupm.com`
  - Scope(s):
    - `com.utilities`
- Open the Unity Package Manager window
- Change the Registry from Unity to `My Registries`
- Add the `Utilities.Async` package

### Via Unity Package Manager and Git url

- Open your Unity Package Manager
- Add package from git url: `https://github.com/RageAgainstThePixel/com.utilities.async.git#upm`

---

## Documentation

## How does it compare to Awaitables in Unity 6?

[Unity 6 introduced Awaitables](https://docs.unity3d.com/6000.2/Documentation/Manual/async-await-support.html) in UnityEngine, which provide similar functionality to Utilities.Async.
Where possible, it is recommended to use the built-in Unity Awaitables for new projects, as they are officially supported and maintained by Unity.

### Example

> [!IMPORTANT]
> Always pass the `destroyCancellationToken` to async methods called from Unity events to avoid long running tasks after the object has been destroyed. This could lead to memory leaks or other unexpected behavior.
>
> For Unity versions prior to 2022.3, you can use the provided snippet to create a `CancellationToken` that is cancelled on `OnDestroy`.
>
> It is also recommended to always encapsulate async methods called from Unity events in try/catch blocks to handle exceptions properly, otherwise they can go unobserved and silently fail.

```csharp
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities.Async;

public class ExampleAsyncScript : MonoBehaviour
{
        // For backwards compatibility with older Unity versions use the following snippet:
#if !UNITY_2022_3_OR_NEWER
        private readonly CancellationTokenSource lifetimeCts = new();
        // ReSharper disable once InconsistentNaming
        private CancellationToken destroyCancellationToken => lifetimeCts.Token;

        private void OnDestroy()
        {
            lifetimeCts?.Cancel();
        }
#endif // !UNITY_2022_3_OR_NEWER

    private async void Start()
    {
        try
        {
            // always encapsulate try/catch around
            // async methods called from unity events
            await MyFunctionAsync(destroyCancellationToken).ConfigureAwait(false);

            // Get back to the main unity thread
            await Awaiters.UnityMainThread;

            // switch to background thread to do a long
            // running process on background thread
            // Not supported on WebGL but only throws a warning.
            await Awaiters.BackgroundThread;

            // an action meant to run on main thread,
            // but invoked from background thread.
            Action backgroundInvokedAction = BackgroundInvokedAction;
            backgroundInvokedAction.InvokeOnMainThread();

            // await on IEnumerator functions as well
            // for backwards compatibility or older code.
            // These can also be called from background threads
            // and the context will switch to main thread
            await MyEnumerableFunction();
            await new WaitForSeconds(1f);
            await new WaitForEndOfFrame();
            await new WaitForFixedUpdate();

            // you can even get progress callbacks for AsyncOperations!
            await SceneManager.LoadSceneAsync(0)
                .WithProgress(new Progress<float>(f => Debug.Log(f)))
                .WithCancellation(destroyCancellationToken);
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    private void BackgroundInvokedAction()
    {
        Debug.Log(Application.dataPath);
    }

    private async Task MyFunctionAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
    }

    private IEnumerator MyEnumerableFunction()
    {
        yield return new WaitForSeconds(1);
        // We can even yield async functions
        // for better interoperability
        yield return MyFunctionAsync();
    }
}
```

### WebGL Support

Shamelessly lifted from <https://github.com/VolodymyrBS/WebGLThreadingPatcher>

WebGL support is now supported, but be aware that long tasks will not run on the background thread and will block the main thread. All tasks will be executed by just one thread so any blocking calls will freeze whole application. Basically it similar to async/await behavior in Blazor.

#### How does it work?

`WebGLPostBuildCallback` uses a IIl2CppProcessor callback to rewrite entries in `mscorelib.dll` and change some method implementations. It changes `ThreadPool` methods that enqueue work items of delegate work to `SynchronizationContext` so all items will be executed in same thread. Also it patches `Timer` implementation to use Javascript timer functionality.
