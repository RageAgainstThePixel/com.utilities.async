// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
namespace Utilities.Async
{
    internal interface IEditorCancelable
    {
        void CancelFromEditor();
    }
}
#endif
