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
        public static PlayModeStateChange CurrentPlayModeState { get; private set; }

        static EditorPlayModeCancellation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                lock (gate)
                {
                    CurrentPlayModeState = state;
                    switch (state)
                    {
                        case PlayModeStateChange.ExitingEditMode:
                        case PlayModeStateChange.ExitingPlayMode:
                            CancelAll();
                            break;
                    }
                }
            };
        }

        public static Registration Register(IEditorCancelable cancelable)
        {
            if (cancelable == null)
            {
                throw new ArgumentNullException(nameof(cancelable));
            }

            lock (gate)
            {
                if (CurrentPlayModeState is PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.EnteredEditMode)
                {
                    activeRegistrations.Add(cancelable);
                }
                else
                {
                    throw new InvalidOperationException(CancellationMessage);
                }
            }

            return new Registration(cancelable);
        }

        private static void CancelAll()
        {
            lock (gate)
            {
                if (activeRegistrations.Count == 0) { return; }

                var activeRegistrationsCopy = new List<IEditorCancelable>(activeRegistrations);

                foreach (var registration in activeRegistrationsCopy)
                {
                    try
                    {
                        registration?.CancelFromEditor();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                activeRegistrationsCopy.Clear();
                activeRegistrations.Clear();
            }
        }

        internal readonly struct Registration : IDisposable
        {
            private readonly IEditorCancelable cancelable;

            public Registration(IEditorCancelable value)
                => cancelable = value;

            public void Dispose()
            {
                if (cancelable == null) { return; }

                lock (gate)
                {
                    activeRegistrations.Remove(cancelable);
                }
            }
        }
    }
}
#endif // UNITY_EDITOR
