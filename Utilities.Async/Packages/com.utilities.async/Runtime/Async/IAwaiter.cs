// Licensed under the MIT License. See LICENSE in the project root for license information.

using JetBrains.Annotations;

namespace Utilities.Async
{
    internal interface IAwaiter
    {
        [UsedImplicitly]
        bool IsCompleted { get; }
    }
}