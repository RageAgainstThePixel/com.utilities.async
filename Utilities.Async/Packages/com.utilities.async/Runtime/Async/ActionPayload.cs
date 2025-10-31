// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace Utilities.Async
{
    internal sealed class ActionPayload
    {
        private static readonly ConcurrentQueue<ActionPayload> pool = new();

        public Action Action;
#if UNITY_EDITOR || DEBUG
        public long EnqueuedUtcTicks;
        public string EnqueuedStackTrace;
#endif

        private ActionPayload() { }

        public static ActionPayload Rent(Action action)
        {
            if (!pool.TryDequeue(out var p))
            {
                p = new ActionPayload();
            }

            p.Action = action;
#if UNITY_EDITOR || DEBUG
            p.EnqueuedUtcTicks = 0;
            p.EnqueuedStackTrace = null;
#endif
            return p;
        }

        public static void Return(ActionPayload p)
        {
            p.Action = null;
#if UNITY_EDITOR || DEBUG
            p.EnqueuedUtcTicks = 0;
            p.EnqueuedStackTrace = null;
#endif
            pool.Enqueue(p);
        }
    }
}
