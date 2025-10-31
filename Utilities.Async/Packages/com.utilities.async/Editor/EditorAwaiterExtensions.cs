// Licensed under the MIT License. See LICENSE in the project root for license information.

using Unity.EditorCoroutines.Editor;

namespace Utilities.Async.Editor
{
    public static class EditorAwaiterExtensions
    {
        public static YieldInstructionAwaiter GetAwaiter(this EditorWaitForSeconds instruction)
            => new(instruction);
    }
}
