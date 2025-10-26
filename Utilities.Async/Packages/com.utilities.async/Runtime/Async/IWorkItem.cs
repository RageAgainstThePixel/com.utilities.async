// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Utilities.Async
{
    internal interface IWorkItem : IAwaiter
    {
        object Result { get; }

        Exception Exception { get; }

        void RegisterContinuation(Action action);
    }
}
