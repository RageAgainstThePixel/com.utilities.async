// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Utilities.Async.Editor
{
    public static class EditorAwaiterExtensions
    {
        public static SimpleCoroutineAwaiter GetAwaiter(this Unity.EditorCoroutines.Editor.EditorWaitForSeconds instruction)
            => AwaiterExtensions.GetAwaiterReturnVoid(instruction);
    }
}
