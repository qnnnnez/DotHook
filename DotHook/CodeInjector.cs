using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotHook
{
    static class CodeInjector
    {
        static public void InjectMethod(TypeDefinition targetClass, MethodDefinition sourceMethod, string methodName=null)
        {
            if (methodName == null)
                methodName = sourceMethod.Name;
            var injectMethod = CloneMethod(sourceMethod);
            targetClass.Methods.Add(injectMethod);
            injectMethod.DeclaringType = targetClass;
            FixReferences(injectMethod);
        }

        static public void HookMethod(MethodDefinition targetMethod, MethodDefinition hookMethod, ReferenceResolver resolver=null, string hookPrefix="__hooked__")
        {
            if(resolver == null)
            {
                resolver = new ReferenceResolver();
                resolver.ScanAssembly(targetMethod.DeclaringType.Module.Assembly);
            }

            var injectMethod = CloneMethod(hookMethod);
            // rename original method
            injectMethod.Name = targetMethod.Name;
            targetMethod.Name = hookPrefix + targetMethod.Name;
            // add the new method
            targetMethod.DeclaringType.Methods.Add(injectMethod);
            // replace the original method with the new one
            resolver.ReplaceAllReferences(targetMethod, hookMethod);
            // process self-calls
            for (int i = 0; i < injectMethod.Body.Instructions.Count; ++i)
            {
                var ins = injectMethod.Body.Instructions[i];
                if (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt)
                {
                    if (ins.Operand == injectMethod)
                    {
                        ins.Operand = targetMethod;
                    }
                }
            }
        }

        static public MethodDefinition CloneMethod(MethodDefinition source)
        {
            var newMethod = new MethodDefinition(source.Name, source.Attributes, source.ReturnType);
            var il = newMethod.Body.GetILProcessor();
            // copy all instructions
            foreach(var sourceIns in source.Body.Instructions)
            {
                var newInstruction = il.Create(sourceIns.OpCode);
                newInstruction.Operand = sourceIns.Operand;
                il.Append(newInstruction);
            }
            // if a instruction whose operand is another instruction, we need to correct its operand
            var newInstructions = newMethod.Body.Instructions;
            foreach (var ins in newInstructions)
            {
                if (ins.Operand is Instruction)
                {
                    ins.Operand = newInstructions[(ins.Operand as Instruction).Offset];
                }
            }
            return newMethod;
        }
        
        static private void FixReferences(MethodDefinition method)
        {
            var module = method.Module;
            for (int i=0; i<method.Body.Instructions.Count; ++i)
            {
                var ins = method.Body.Instructions[i];
                if (ins.Operand is MethodReference)
                {
                    ins.Operand = module.ImportReference(ins.Operand as MethodReference);
                }
                else if (ins.Operand is TypeReference)
                {
                    ins.Operand = module.ImportReference(ins.Operand as TypeReference);
                }
                else if (ins.Operand is FieldReference)
                {
                    ins.Operand = module.ImportReference(ins.Operand as FieldReference);
                }
            }
        }
    }
}
