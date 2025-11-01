// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Utilities.Async
{
    public sealed class BackgroundThread : CustomYieldInstruction
    {
        public override bool keepWaiting => false;
    }
}
