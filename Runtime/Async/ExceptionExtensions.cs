// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Utilities.Async
{
    internal static class ExceptionExtensions
    {
        public static Exception GenerateExceptionTrace(this IEnumerable<IEnumerator> enumerators, Exception e)
        {
            var objectTrace = enumerators.GenerateObjectTrace();
            return objectTrace.Any() ? new Exception(objectTrace.GenerateObjectTraceMessage(), e) : e;
        }

        public static List<Type> GenerateObjectTrace(this IEnumerable<IEnumerator> enumerators)
        {
            var objTrace = new List<Type>();

            foreach (var enumerator in enumerators)
            {
                // NOTE: This only works with scripting engine 4.6
                // And could easily stop working with unity updates
                var field = enumerator.GetType().GetField("$this", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (field == null) { continue; }

                var obj = field.GetValue(enumerator);

                if (obj == null) { continue; }

                var objType = obj.GetType();

                if (!objTrace.Any() || objType != objTrace.Last())
                {
                    objTrace.Add(objType);
                }
            }

            objTrace.Reverse();
            return objTrace;
        }

        public static string GenerateObjectTraceMessage(this List<Type> objTrace)
        {
            var result = new StringBuilder();

            foreach (var objType in objTrace)
            {
                if (result.Length != 0)
                {
                    result.Append("\n -> ");
                }

                result.Append(objType);
            }

            result.AppendLine();
            return $"Unity Coroutine Object Trace: {result}";
        }
    }
}