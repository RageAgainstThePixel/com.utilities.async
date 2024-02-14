#if UNITY_WEBGL

//    MIT License
//    Copyright (c) 2017 - 2021 Mattias Edlund
//    Permission is hereby granted, free of charge, to any person obtaining a copy
//    of this software and associated documentation files (the "Software"), to deal
//    in the Software without restriction, including without limitation the rights
//    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//    copies of the Software, and to permit persons to whom the Software is
//    furnished to do so, subject to the following conditions:

//    The above copyright notice and this permission notice shall be included in all
//    copies or substantial portions of the Software.

//    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

//    https://github.com/VolodymyrBS/WebGLThreadingPatcher

using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Utilities.Async.Editor.WebGL
{
    internal class WebGLPostBuildCallback : IPostBuildPlayerScriptDLLs
    {
        public int callbackOrder => 0;

        public void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) { return; }

#if UNITY_2022_1_OR_NEWER
            var mscorelibPath = report.GetFiles().FirstOrDefault(file => file.path.EndsWith("mscorlib.dll")).path;
#else
            var mscorelibPath = report.files.FirstOrDefault(file => file.path.EndsWith("mscorlib.dll")).path;
#endif

            if (string.IsNullOrWhiteSpace(mscorelibPath))
            {
                Debug.LogError("Can't find mscorlib.dll in build dll files");
                return;
            }

            using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(mscorelibPath), new ReaderParameters(ReadingMode.Immediate) { ReadWrite = true });
            var mainModule = assembly.MainModule;

            if (!TryGetTypes(mainModule, out var threadPool, out var synchronizationContext, out var postCallback, out var waitCallback, out var taskExecutionItem, out var timeScheduler))
            {
                return;
            }

            PatchThreadPool(mainModule, threadPool, synchronizationContext, postCallback, waitCallback, taskExecutionItem);
            assembly.Write();
        }

        private static void PatchThreadPool(ModuleDefinition mainModule, TypeDefinition threadPool, TypeDefinition synchronizationContext, TypeDefinition postCallback, TypeDefinition waitCallback, TypeDefinition threadPoolWorkItem)
        {
            var taskExecutionCallback = AddTaskExecutionPostCallback(threadPool, threadPoolWorkItem, mainModule);

            foreach (var methodDefinition in threadPool.Methods)
            {
                switch (methodDefinition.Name)
                {
                    case "QueueUserWorkItem" when methodDefinition.HasGenericParameters:
                    case "UnsafeQueueUserWorkItem" when methodDefinition.HasGenericParameters:
                        PatchQueueUserWorkItemGeneric(mainModule, methodDefinition, synchronizationContext, waitCallback, postCallback);
                        break;
                    case "QueueUserWorkItem":
                    case "UnsafeQueueUserWorkItem":
                        PatchQueueUserWorkItem(mainModule, methodDefinition, synchronizationContext, waitCallback, postCallback);
                        break;
                    case "UnsafeQueueCustomWorkItem":
                        PatchUnsafeQueueCustomWorkItem(mainModule, methodDefinition, synchronizationContext, taskExecutionCallback, postCallback);
                        break;
                    case "TryPopCustomWorkItem":
                        PatchTryPopCustomWorkItem(methodDefinition);
                        break;
                    case "GetAvailableThreads":
                    case "GetMaxThreads":
                    case "GetMinThreads":
                        PatchGetThreads(methodDefinition);
                        break;
                    case "SetMaxThreads":
                    case "SetMinThreads":
                        PatchSetThreads(methodDefinition);
                        break;
                }
            }
        }

        private static TypeDefinition GetGenericToObjectDelegateWrapper(ModuleDefinition moduleDefinition)
        {
            const string Namespace = "System.Threading";
            const string ClassName = "<>_GenericWrapper";

            if (moduleDefinition.Types.FirstOrDefault(t => t.Namespace == Namespace && t.Name == ClassName) is { } wrapper)
            {
                return wrapper;
            }

            var genericWrapper = new TypeDefinition(Namespace, ClassName, TypeAttributes.AnsiClass | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);
            var genericParameter = new GenericParameter("T", genericWrapper);
            genericWrapper.GenericParameters.Add(genericParameter);

            var (actionOfT, callbackField) = CreateCallbackField(moduleDefinition, genericWrapper, genericParameter);
            var ctor = CreateConstructor(moduleDefinition, callbackField);
            var wrapMethod = CreateInvokeMethod(moduleDefinition, genericParameter, actionOfT, callbackField);

            genericWrapper.Methods.Add(ctor);
            genericWrapper.Methods.Add(wrapMethod);

            moduleDefinition.Types.Add(genericWrapper);
            return genericWrapper;

            static (TypeReference, FieldReference) CreateCallbackField(ModuleDefinition moduleDefinition, TypeDefinition genericWrapper, GenericParameter genericParameter)
            {
                var actionType = moduleDefinition.Types.First(t => t.FullName == "System.Action`1" && t.GenericParameters.Count == 1);
                var actionOfT = new GenericInstanceType(actionType);
                actionOfT.GenericArguments.Add(genericParameter);
                FieldDefinition callback = new FieldDefinition("callback", FieldAttributes.Public, actionOfT);
                genericWrapper.Fields.Add(callback);

                var wrapperOfT = new GenericInstanceType(genericWrapper);
                wrapperOfT.GenericArguments.Add(genericParameter);
                return (actionOfT, new FieldReference(callback.Name, actionOfT, wrapperOfT));
            }

            static MethodDefinition CreateInvokeMethod(ModuleDefinition moduleDefinition, GenericParameter genericParameter, TypeReference actionOfT, FieldReference callbackField)
            {
                var wrapMethod = new MethodDefinition("Invoke", MethodAttributes.Public, moduleDefinition.TypeSystem.Void);
                wrapMethod.Parameters.Add(new ParameterDefinition(moduleDefinition.TypeSystem.Object) { Name = "state" });
                var ilProcessor = wrapMethod.Body.GetILProcessor();
                ilProcessor.Emit(OpCodes.Ldarg_0);
                ilProcessor.Emit(OpCodes.Ldfld, callbackField);
                ilProcessor.Emit(OpCodes.Ldarg_1);
                ilProcessor.Emit(OpCodes.Unbox_Any, genericParameter);
                var invokeMethod = new MethodReference("Invoke", moduleDefinition.TypeSystem.Void, actionOfT)
                {
                    HasThis = true
                };
                invokeMethod.Parameters.Add(new ParameterDefinition(genericParameter));
                ilProcessor.Emit(OpCodes.Callvirt, invokeMethod);
                ilProcessor.Emit(OpCodes.Ret);

                return wrapMethod;
            }

            static MethodDefinition CreateConstructor(ModuleDefinition moduleDefinition, FieldReference callbackField)
            {
                var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig, moduleDefinition.TypeSystem.Void);
                ctor.Parameters.Add(new ParameterDefinition(callbackField.FieldType));
                var ilProcessor = ctor.Body.GetILProcessor();
                ilProcessor.Emit(OpCodes.Ldarg_0);
                ilProcessor.Emit(OpCodes.Call, new MethodReference(".ctor", moduleDefinition.TypeSystem.Void, moduleDefinition.TypeSystem.Object) { HasThis = true });
                ilProcessor.Emit(OpCodes.Ldarg_0);
                ilProcessor.Emit(OpCodes.Ldarg_1);
                ilProcessor.Emit(OpCodes.Stfld, callbackField);
                ilProcessor.Emit(OpCodes.Ret);
                return ctor;
            }
        }

        private static void PatchQueueUserWorkItemGeneric(ModuleDefinition moduleDefinition, MethodDefinition methodDefinition, TypeDefinition synchronizationContext, TypeDefinition waitCallback, TypeDefinition postCallback)
        {
            var genericWrapper = GetGenericToObjectDelegateWrapper(moduleDefinition);
            var wrapperOfT = new GenericInstanceType(genericWrapper);
            wrapperOfT.GenericArguments.Add(methodDefinition.GenericParameters[0]);

            var ilProcessor = methodDefinition.Body.GetILProcessor();
            ilProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();

            var actionType = moduleDefinition.Types.First(t => t.FullName == "System.Action`1" && t.GenericParameters.Count == 1);
            var actionOfT = new GenericInstanceType(actionType);
            actionOfT.GenericArguments.Add(genericWrapper.GenericParameters[0]);

            ilProcessor.Emit(OpCodes.Ldarg_0);
            var wrapperCtor = new MethodReference(".ctor", moduleDefinition.TypeSystem.Void, wrapperOfT);
            wrapperCtor.Parameters.Add(new ParameterDefinition(actionOfT));
            wrapperCtor.HasThis = true;
            ilProcessor.Emit(OpCodes.Newobj, wrapperCtor);

            var wrapperInvoke = new MethodReference("Invoke", moduleDefinition.TypeSystem.Void, wrapperOfT);
            wrapperInvoke.Parameters.Add(new ParameterDefinition(moduleDefinition.TypeSystem.Object));
            wrapperInvoke.HasThis = true;
            ilProcessor.Emit(OpCodes.Ldftn, wrapperInvoke);

            ilProcessor.Emit(OpCodes.Newobj, waitCallback.Methods.First(m => m.IsConstructor && m.Parameters.Count == 2));

            ilProcessor.Emit(OpCodes.Ldarg_1);
            ilProcessor.Emit(OpCodes.Box, methodDefinition.GenericParameters[0]);
            var notGenericVariant = new MethodReference(methodDefinition.Name, methodDefinition.ReturnType, methodDefinition.DeclaringType);
            notGenericVariant.Parameters.Add(new ParameterDefinition(moduleDefinition.Types.First(t => t.FullName == "System.Threading.WaitCallback")));
            notGenericVariant.Parameters.Add(new ParameterDefinition(moduleDefinition.TypeSystem.Object));
            ilProcessor.Emit(OpCodes.Call, notGenericVariant);


            ilProcessor.Emit(OpCodes.Ret);
        }

        private static void PatchQueueUserWorkItem(ModuleDefinition moduleDefinition, MethodDefinition methodDefinition, TypeDefinition synchronizationContext, TypeDefinition waitCallback, TypeDefinition postCallback)
        {
            var ilProcessor = methodDefinition.Body.GetILProcessor();
            ilProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            ilProcessor.Emit(OpCodes.Call, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "get_Current")));
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Ldftn, moduleDefinition.ImportReference(waitCallback.Methods.Single(s => s.Name == "Invoke")));
            ilProcessor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(postCallback.Methods.First(s => s.IsConstructor)));

            if (methodDefinition.Parameters.Count == 2)
            {
                ilProcessor.Emit(OpCodes.Ldarg_1);
            }
            else
            {
                ilProcessor.Emit(OpCodes.Ldnull);
            }

            ilProcessor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "Post")));

            ilProcessor.Emit(OpCodes.Ldc_I4_1);
            ilProcessor.Emit(OpCodes.Ret);
        }

        private static void PatchUnsafeQueueCustomWorkItem(ModuleDefinition moduleDefinition, MethodDefinition methodDefinition, TypeDefinition synchronizationContext, MethodDefinition taskExecutionCallcack, TypeDefinition postCallback)
        {
            var ilProcessor = methodDefinition.Body.GetILProcessor();
            ilProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            ilProcessor.Emit(OpCodes.Call, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "get_Current")));
            ilProcessor.Emit(OpCodes.Ldnull);
            ilProcessor.Emit(OpCodes.Ldftn, moduleDefinition.ImportReference(taskExecutionCallcack));
            ilProcessor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(postCallback.Methods.First(s => s.IsConstructor)));
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(synchronizationContext.Methods.Single(s => s.Name == "Post")));
            ilProcessor.Emit(OpCodes.Ret);
        }

        private static void PatchTryPopCustomWorkItem(MethodDefinition methodDefinition)
        {
            var ilPProcessor = methodDefinition.Body.GetILProcessor();
            ilPProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            ilPProcessor.Emit(OpCodes.Ldc_I4_0);
            ilPProcessor.Emit(OpCodes.Ret);
        }

        private static void PatchGetThreads(MethodDefinition methodDefinition)
        {
            var ilPProcessor = methodDefinition.Body.GetILProcessor();
            ilPProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            ilPProcessor.Emit(OpCodes.Ldarg_0);
            ilPProcessor.Emit(OpCodes.Ldc_I4_1);
            ilPProcessor.Emit(OpCodes.Stind_I4);
            ilPProcessor.Emit(OpCodes.Ldarg_1);
            ilPProcessor.Emit(OpCodes.Ldc_I4_1);
            ilPProcessor.Emit(OpCodes.Stind_I4);
            ilPProcessor.Emit(OpCodes.Ret);
        }

        private static void PatchSetThreads(MethodDefinition methodDefinition)
        {
            var ilPProcessor = methodDefinition.Body.GetILProcessor();
            ilPProcessor.Body.Instructions.Clear();
            methodDefinition.Body.ExceptionHandlers.Clear();
            var falseRet = ilPProcessor.Create(OpCodes.Ldc_I4_0);

            ilPProcessor.Emit(OpCodes.Ldarg_0);
            ilPProcessor.Emit(OpCodes.Ldc_I4_1);
            ilPProcessor.Emit(OpCodes.Bne_Un_S, falseRet);

            ilPProcessor.Emit(OpCodes.Ldarg_1);
            ilPProcessor.Emit(OpCodes.Ldc_I4_1);
            ilPProcessor.Emit(OpCodes.Ceq);
            ilPProcessor.Emit(OpCodes.Ret);

            ilPProcessor.Append(falseRet);
            ilPProcessor.Emit(OpCodes.Ret);
        }

        private static MethodDefinition AddTaskExecutionPostCallback(TypeDefinition threadPool, TypeDefinition taskExecutionItem, ModuleDefinition moduleDefinition)
        {
            var method = new MethodDefinition("TaskExecutionItemExecute",
                   MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig,
                   moduleDefinition.TypeSystem.Void);

            method.Parameters.Add(new ParameterDefinition("state", ParameterAttributes.None, moduleDefinition.TypeSystem.Object));

            var ilProcessor = method.Body.GetILProcessor();
            ilProcessor.Emit(OpCodes.Ldarg_0);
            ilProcessor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(taskExecutionItem.Methods.Single(s => s.Name == "ExecuteWorkItem")));
            ilProcessor.Emit(OpCodes.Ret);

            threadPool.Methods.Add(method);

            return method;
        }

        private static bool TryGetTypes(ModuleDefinition moduleDefinition,
            out TypeDefinition threadPool,
            out TypeDefinition synchronizationContext,
            out TypeDefinition sendOrPostCallback,
            out TypeDefinition waitCallback,
            out TypeDefinition threadPoolWorkItem,
            out TypeDefinition timerScheduler)
        {
            threadPool = null;
            synchronizationContext = null;
            sendOrPostCallback = null;
            waitCallback = null;
            threadPoolWorkItem = null;
            timerScheduler = null;

            foreach (var type in moduleDefinition.Types)
            {
                switch (type.FullName)
                {
                    case "System.Threading.ThreadPool":
                        threadPool = type;
                        break;
                    case "System.Threading.SynchronizationContext":
                        synchronizationContext = type;
                        break;
                    case "System.Threading.SendOrPostCallback":
                        sendOrPostCallback = type;
                        break;
                    case "System.Threading.WaitCallback":
                        waitCallback = type;
                        break;
                    case "System.Threading.IThreadPoolWorkItem":
                        threadPoolWorkItem = type;
                        break;
                    case "System.Threading.Timer":
                    {
                        foreach (var nested in type.NestedTypes.Where(nested => nested.FullName.Contains("Scheduler")))
                        {
                            timerScheduler = nested;
                        }

                        break;
                    }
                }
            }

            return
                CheckTypeAssigned("System.Threading.ThreadPool", threadPool) &&
                CheckTypeAssigned("System.Threading.SynchronizationContext", synchronizationContext) &&
                CheckTypeAssigned("System.Threading.SendOrPostCallback", sendOrPostCallback) &&
                CheckTypeAssigned("System.Threading.WaitCallback", waitCallback) &&
                CheckTypeAssigned("System.Threading.IThreadPoolWorkItem", threadPoolWorkItem) &&
                CheckTypeAssigned("System.Threading.Timer.Scheduler", timerScheduler);

            static bool CheckTypeAssigned(string name, TypeDefinition type)
            {
                if (type != null)
                {
                    return true;
                }

                Debug.LogError($"Can't find {name}");
                return false;
            }
        }
    }
}

#endif // UNITY_WEBGL
