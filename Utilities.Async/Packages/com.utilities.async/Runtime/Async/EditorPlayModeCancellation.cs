// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Utilities.Async
{
    internal static class EditorPlayModeCancellation
    {
        public const string CancellationMessage = "Operation cancelled due to Unity Editor play mode state change.";

        private static readonly object gate = new();
        private static readonly HashSet<IEditorCancelable> activeRegistrations = new();

        static EditorPlayModeCancellation()
            => EditorApplication.playModeStateChanged += _ => CancelAll();

        public static IDisposable Register(IEditorCancelable cancelable)
        {
            if (cancelable == null)
            {
                throw new ArgumentNullException(nameof(cancelable));
            }

            lock (gate)
            {
                activeRegistrations.Add(cancelable);
            }

            return new Registration(cancelable);
        }

        private static void CancelAll()
        {
            IEditorCancelable[] snapshot;

            lock (gate)
            {
                if (activeRegistrations.Count == 0) { return; }
                snapshot = new IEditorCancelable[activeRegistrations.Count];
                activeRegistrations.CopyTo(snapshot);
            }

            foreach (var registration in snapshot)
            {
                try
                {
                    registration?.CancelFromEditor();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private IEditorCancelable cancelable;

            public Registration(IEditorCancelable value)
                => cancelable = value;

            public void Dispose()
            {
                if (cancelable == null) { return; }

                lock (gate)
                {
                    activeRegistrations.Remove(cancelable);
                }

                cancelable = null;
            }
        }
    }
}
#endif
