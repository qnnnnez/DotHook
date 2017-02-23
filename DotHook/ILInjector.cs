using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;

namespace DotHook
{
    static class ILInjector
    {
        static public void HookBeforeCallStart(MethodDefinition targetMethod, MethodDefinition injectingMethod)
        {
            var instructions = injectingMethod.Body.Instructions;
            var targetBody = targetMethod.Body;
            var il = targetBody.GetILProcessor();
            var targetFirst = targetBody.Instructions.First();

            targetBody.MaxStackSize = Math.Max(targetBody.MaxStackSize, injectingMethod.Body.MaxStackSize);

            FixLocals(targetMethod, injectingMethod);
            var injectings = ProcessILs(targetMethod, injectingMethod, targetFirst);

            for (var i = 0; i < injectings.Count; ++i)
            {
                var inject = injectings[i];
                il.InsertBefore(targetFirst, inject);
            }
        }

        static public void HookBeforeCallReturn(MethodDefinition targetMethod, MethodDefinition injectingMethod)
        {
            var instructions = injectingMethod.Body.Instructions;
            var targetBody = targetMethod.Body;
            var il = targetBody.GetILProcessor();
            var targetFirst = targetBody.Instructions.First();
            var oldInstructionLength = targetBody.Instructions.Count();

            targetBody.MaxStackSize = Math.Max(targetBody.MaxStackSize, injectingMethod.Body.MaxStackSize);

            FixLocals(targetMethod, injectingMethod);
            var injectings = ProcessILs(targetMethod, injectingMethod, null);

            for (var i = 0; i < injectings.Count; ++i)
            {
                var inject = injectings[i];
                il.Append(inject);
            }

            if (targetMethod.ReturnType == TypeInspector.GetDefinitionByType(typeof(void)))
                FixReturn(targetMethod, injectingMethod, oldInstructionLength);
            else
                FixReturnValue(targetMethod, injectingMethod, oldInstructionLength);
        }

        static List<Instruction> ProcessILs(MethodDefinition targetMethod, MethodDefinition injectingMethod, Instruction returnPos = null)
        {
            var instructions = injectingMethod.Body.Instructions.ToList();
            var module = targetMethod.Module;

            foreach (var ins in instructions)
            {
                if (ins.OpCode == OpCodes.Ret)
                {
                    // stop executing hook instructions
                    if (returnPos != null)
                    {
                        ins.OpCode = OpCodes.Br;
                        ins.Operand = returnPos;
                    }
                }
                else if (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Calli || ins.OpCode == OpCodes.Callvirt)
                {
                    // fix metadata
                    var method = ins.Operand as MethodReference;
                    ins.Operand = module.ImportReference(method);
                }
                else if (ins.OpCode == OpCodes.Box || ins.OpCode == OpCodes.Unbox)
                {
                    // fix metadata
                    var type = ins.Operand as TypeReference;
                    ins.Operand = module.ImportReference(type);
                }
                else if (ins.OpCode == OpCodes.Newobj)
                {
                    // fix metadata
                    var method = ins.Operand as MethodReference;
                    ins.Operand = module.ImportReference(method);
                }
                else if (ins.OpCode == OpCodes.Castclass)
                {
                    // fix metadata
                    var type = ins.Operand as TypeReference;
                    ins.Operand = module.ImportReference(type);
                }
                else if (ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Ldflda || ins.OpCode == OpCodes.Ldsfld || ins.OpCode == OpCodes.Stfld || ins.OpCode == OpCodes.Stsfld)
                {
                    var field = ins.Operand as FieldReference;
                    ins.Operand = module.ImportReference(field);
                }
            }

            return instructions;
        }

        static void FixLocals(MethodDefinition targetMethod, MethodDefinition injectingMethod)
        {
            var oldVarCount = targetMethod.Body.Variables.Count;
            foreach (var local in injectingMethod.Body.Variables)
            {
                var newLocal = new VariableDefinition(targetMethod.Module.ImportReference(local.VariableType));
                targetMethod.Body.Variables.Add(newLocal);
            }

            Func<int, VariableDefinition> getNewVar;
            getNewVar = (int x) => targetMethod.Body.Variables[oldVarCount + x];
            var instructions = injectingMethod.Body.Instructions.ToList();
            var module = targetMethod.Module;

            foreach (var ins in instructions)
            {
                if (ins.OpCode == OpCodes.Ldloc_0 || ins.OpCode == OpCodes.Ldloc_1 || ins.OpCode == OpCodes.Ldloc_2 || ins.OpCode == OpCodes.Ldloc_3)
                {
                    // expand to long form and offset
                    ins.Operand = getNewVar(ins.OpCode.Value - OpCodes.Ldloc_0.Value);
                    ins.OpCode = OpCodes.Ldloc;
                }
                else if (ins.OpCode == OpCodes.Ldloc_S)
                {
                    // expand to long form and offset
                    ins.OpCode = OpCodes.Ldloc;
                    ins.Operand = getNewVar((ins.Operand as VariableDefinition).Index);
                }
                else if (ins.OpCode == OpCodes.Ldloca_S)
                {
                    // expand to long form and offset
                    ins.OpCode = OpCodes.Ldloca;
                    ins.Operand = getNewVar((ins.Operand as VariableDefinition).Index);
                }
                else if (ins.OpCode == OpCodes.Ldloc)
                {
                    // expand to long form and offset
                    ins.Operand = getNewVar((ins.Operand as VariableDefinition).Index);
                }
                else if (ins.OpCode == OpCodes.Stloc_0 || ins.OpCode == OpCodes.Stloc_1 || ins.OpCode == OpCodes.Stloc_2 || ins.OpCode == OpCodes.Stloc_3)
                {
                    // expand to long form and offset
                    ins.Operand = getNewVar(ins.OpCode.Value - OpCodes.Stloc_0.Value);
                    ins.OpCode = OpCodes.Stloc;
                }
                else if (ins.OpCode == OpCodes.Stloc_S)
                {
                    // expand to long form and offset
                    ins.OpCode = OpCodes.Stloc;
                    ins.Operand = getNewVar((ins.Operand as VariableDefinition).Index);
                }
                else if (ins.OpCode == OpCodes.Stloc)
                {
                    // expand to long form and offset
                    ins.Operand = getNewVar((ins.Operand as VariableDefinition).Index);
                }
            }
        }

        static void FixReturn(MethodDefinition targetMethod, MethodDefinition injectingMethod, int oldInstructionLength)
        {
            // for void-returning methods
            
            var oldEnd = targetMethod.Body.Instructions[oldInstructionLength - 1];
            oldEnd.OpCode = OpCodes.Nop;
            foreach (var i in targetMethod.Body.Instructions)
            {
                if (i == oldEnd)
                    break;
                if (i.OpCode == OpCodes.Ret)
                {
                    i.OpCode = OpCodes.Br;
                    i.Operand = oldEnd;
                }
            }
        }

        static void FixReturnValue(MethodDefinition targetMethod, MethodDefinition injectingMethod, int oldInstructionLength)
        {
            if (targetMethod.Parameters.Count + 1 != injectingMethod.Parameters.Count)
                throw new ArgumentException("Parameter argument count not match!");
            
            var instructions = injectingMethod.Body.Instructions.ToList();
            var returnValueLocal = new VariableDefinition(targetMethod.ReturnType);
            targetMethod.Body.Variables.Add(returnValueLocal);

            var oldEnd = targetMethod.Body.Instructions[oldInstructionLength - 1];
            oldEnd.OpCode = OpCodes.Stloc;
            oldEnd.Operand = returnValueLocal;

            for (var i = 0; i < oldInstructionLength-1; ++i)
            {
                var ins = targetMethod.Body.Instructions[i];
                if (ins.OpCode == OpCodes.Ret)
                {
                    ins.OpCode = OpCodes.Br;
                    ins.Operand = oldEnd;
                }
            }

            for(var i=oldInstructionLength; i< targetMethod.Body.Instructions.Count; ++i)
            {
                var ins = targetMethod.Body.Instructions[i];
                // expand to long form
                if (ins.OpCode == OpCodes.Ldarg_0 || ins.OpCode == OpCodes.Ldarg_1 || ins.OpCode == OpCodes.Ldarg_2 || ins.OpCode == OpCodes.Ldarg_3)
                {
                    var index = ins.OpCode.Value - OpCodes.Ldarg_0.Value;
                    if (!targetMethod.IsStatic)
                    {
                        if (index == 0)
                            continue;
                        else
                        {
                            index -= 1;
                        }
                    }
                    if (index == targetMethod.Parameters.Count)
                    {
                        ins.OpCode = OpCodes.Ldloc;
                        ins.Operand = returnValueLocal;
                    }
                    else
                    {
                        ins.OpCode = OpCodes.Ldarg;
                        ins.Operand = targetMethod.Parameters[index];
                    }
                }
                else if (ins.OpCode == OpCodes.Ldarga_S)
                {
                    var index = (ins.Operand as ParameterDefinition).Index;
                    if (index == targetMethod.Parameters.Count)
                    {
                        ins.OpCode = OpCodes.Ldloca;
                        ins.Operand = returnValueLocal;
                    }
                    else
                    {
                        ins.OpCode = OpCodes.Ldarga;
                        ins.Operand = targetMethod.Parameters[index];
                    }
                }
                else if (ins.OpCode == OpCodes.Ldarg_S)
                {
                    var index = (ins.Operand as ParameterDefinition).Index;
                    if (index == targetMethod.Parameters.Count)
                    {
                        ins.OpCode = OpCodes.Ldloc;
                        ins.Operand = returnValueLocal;
                    }
                    else
                    {
                        ins.OpCode = OpCodes.Ldarg;
                        ins.Operand = targetMethod.Parameters[index];
                    }
                }
                else if (ins.OpCode == OpCodes.Starg_S)
                {
                    var index = (ins.Operand as ParameterDefinition).Index;
                    if (index == targetMethod.Parameters.Count)
                    {
                        ins.OpCode = OpCodes.Stloc;
                        ins.Operand = returnValueLocal;
                    }
                    else
                    {
                        ins.OpCode = OpCodes.Starg;
                        ins.Operand = targetMethod.Parameters[index];
                    }
                }
                else if (ins.OpCode == OpCodes.Starg)
                {
                    var index = (ins.Operand as ParameterDefinition).Index;
                    if (index == targetMethod.Parameters.Count)
                    {
                        ins.OpCode = OpCodes.Stloc;
                        ins.Operand = returnValueLocal;
                    }
                    else
                    {
                        ins.Operand = targetMethod.Parameters[index];
                    }
                }
            }
        }

        static void PerformMagic(MethodDefinition targetMethod, MethodDefinition injectingMethod)
        {
        }
    }
}
