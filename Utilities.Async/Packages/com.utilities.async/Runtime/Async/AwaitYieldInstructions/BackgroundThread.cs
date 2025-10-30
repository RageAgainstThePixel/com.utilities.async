// MIT License

// Copyright(c) 2016 Modest Tree Media Inc

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#if !UNITY_6000_0_OR_NEWER
using UnityEngine;

#if !UNITY_WEBGL || UNITY_EDITOR

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

#endif

namespace Utilities.Async
{
    /// <summary>
    /// Helper class for continuing executions on a background thread.
    /// </summary>
    public sealed class BackgroundThread : CustomYieldInstruction
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter()
        {
            return Task.Run(async () =>
            {
                while (SyncContextUtility.IsMainThread)
                {
                    await Task.Yield();
                }
            }).ConfigureAwait(false).GetAwaiter();
        }
#endif // !UNITY_WEBGL || UNITY_EDITOR
        public override bool keepWaiting => false;
    }
}
#endif // !UNITY_6000_0_OR_NEWER
