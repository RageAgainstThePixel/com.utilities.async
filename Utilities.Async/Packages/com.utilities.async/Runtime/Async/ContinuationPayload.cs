// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;

namespace Utilities.Async
{
    internal sealed class ContinuationPayload
    {
        private static readonly ConcurrentQueue<ContinuationPayload> payloadPool = new();

        public Action<object> Action;
        public object State;

        private ContinuationPayload() { }

        public static ContinuationPayload Rent(Action<object> action, object state)
        {
            if (!payloadPool.TryDequeue(out var payload))
            {
                payload = new ContinuationPayload();
            }

            payload.Action = action;
            payload.State = state;
            return payload;
        }

        public static void Return(ContinuationPayload payload)
        {
            payload.Action = null;
            payload.State = null;
            payloadPool.Enqueue(payload);
        }
    }
}
