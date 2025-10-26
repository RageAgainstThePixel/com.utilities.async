// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;

namespace Utilities.Async
{
    internal sealed class InstructionWrapper : IEnumerator
    {
        private object instruction = default;
        private int state;

        public object Current => state == 1 ? instruction : null;

        public bool MoveNext()
        {
            switch (state)
            {
                case 0:
                    state = 1;
                    return true;
                case 1:
                    state = 2;
                    instruction = null;
                    return false;
                default:
                    return false;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
            instruction = null;
            state = 0;
        }

        public void Initialize(object value)
        {
            instruction = value;
            state = 0;
        }

        public void Clear()
        {
            instruction = null;
            state = 0;
        }
    }
}
