// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Utilities.Async
{
    internal readonly struct CoroutineWrapper
    {
        public readonly object Coroutine;

        public CoroutineWrapper(object coroutine)
        {
            Coroutine = coroutine;
        }
    }
}
