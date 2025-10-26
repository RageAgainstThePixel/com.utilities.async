// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Utilities.Async
{
    internal sealed class ContinuationDispatcher
    {
        private Action continuation;

        public ContinuationDispatcher()
            => InvokeAction = Invoke;

        public Action InvokeAction { get; }

        public void Set(Action action)
            => continuation = action;

        public void Clear()
            => continuation = null;

        public void Invoke()
        {
            var action = continuation;
            if (action == null) { return; }
            continuation = null;
            SyncContextUtility.RunOnUnityThread(action);
        }
    }
}
