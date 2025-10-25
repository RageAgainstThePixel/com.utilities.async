// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Utilities.Async.Internal;

namespace Utilities.Async
{
    public static class ActionExtensions
    {
        /// <summary>
        /// Invokes the <see cref="Action"/> on the unity main thread.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        public static void InvokeOnMainThread(this Action action)
            => SyncContextUtility.RunOnUnityThread(action);
    }
}
