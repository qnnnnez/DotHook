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
        static public MethodDefinition InjectMethod(TypeDefinition targetClass, MethodDefinition sourceMethod, string methodName=null)
        {
            if (methodName == null)
                methodName = sourceMethod.Name;
            var injectMethod = CloneMethod(sourceMethod);
            targetClass.Methods.Add(injectMethod);
            injectMethod.DeclaringType = targetClass;
            FixReferences(injectMethod);
            return injectMethod;
        }

        static public void HookMethod(MethodDefinition targetMethod, MethodDefinition hookMethod, ReferenceResolver resolver=null, string hookPrefix="__hooked__")
        {
            if(resolver == null)
            {
                resolver = new ReferenceResolver();
                resolver.ScanAssembly(targetMethod.DeclaringType.Module.Assembly);
            }

            var injectMethod = InjectMethod(targetMethod.DeclaringType, hookMethod);
            injectMethod.Attributes = targetMethod.Attributes;
            // rename original method
            injectMethod.Name = targetMethod.Name;
            targetMethod.Name = hookPrefix + targetMethod.Name;
            // replace the original method with the new one
            resolver.ReplaceAllReferences(targetMethod, injectMethod);
            // process self-calls
            for (int i = 0; i < injectMethod.Body.Instructions.Count; ++i)
            {
                var ins = injectMethod.Body.Instructions[i];
                if (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt)
                {
                    if ((ins.Operand as MethodReference).FullName == hookMethod.FullName)
                    {
                        ins.Operand = targetMethod;
                    }
                }
            }
        }

        static public MethodDefinition CloneMethod(MethodDefinition source)
        {
            var newMethod = new MethodDefinition(source.Name, source.Attributes, source.ReturnType);
            
            foreach (var param in source.Parameters)
                newMethod.Parameters.Add(param);
            foreach (var variable in source.Body.Variables)
                newMethod.Body.Variables.Add(variable);
            var il = newMethod.Body.GetILProcessor();
            // copy all instructions
            foreach(var sourceIns in source.Body.Instructions)
            {
                var newInstruction = il.Create(OpCodes.Nop);
                newInstruction.OpCode = sourceIns.OpCode;
                newInstruction.Operand = sourceIns.Operand;
                il.Append(newInstruction);
            }
            // if a instruction whose operand is another instruction, we need to correct its operand
            var newInstructions = newMethod.Body.Instructions;
            foreach (var ins in newInstructions)
            {
                if (ins.Operand is Instruction)
                {
                    ins.Operand = newInstructions[source.Body.Instructions.IndexOf(ins.Operand as Instruction)];
                }
            }
            return newMethod;
        }
        
        static private void FixReferences(MethodDefinition method)
        {
            var module = method.Module;

            method.ReturnType = module.ImportReference(method.ReturnType);

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

            foreach (var param in method.Parameters)
                param.ParameterType = module.ImportReference(param.ParameterType);
            foreach (var variable in method.Body.Variables)
                variable.VariableType = module.ImportReference(variable.VariableType);
        }
    }
}
