// Licensed under the MIT License. See LICENSE in the project root for license information.

using Unity.EditorCoroutines.Editor;

namespace Utilities.Async.Editor
{
    public static class EditorAwaiterExtensions
    {
        public static CoroutineAwaiter GetAwaiter(this EditorWaitForSeconds instruction)
            => new CoroutineAwaiter(instruction);
    }
}
