using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotHook
{
    class ReferenceResolver
    {
        public class InstructionResolveResult
        {
            public MethodDefinition method;
            public Instruction instruction;
            public InstructionResolveResult(MethodDefinition m, Instruction ins)
            {
                method = m;
                instruction = ins;
            }
        }

        public interface ITypeResolveResult { }

        public class FieldTypeResolveResult : ITypeResolveResult
        {
            public FieldDefinition field;
            public FieldTypeResolveResult(FieldDefinition f) { field = f; }
        }
        public class ReturnTypeTypeResolveResult : ITypeResolveResult
        {
            public MethodDefinition method;
            public ReturnTypeTypeResolveResult(MethodDefinition m) { method = m; }
        }
        public class ParameterTypeResolveResult : ITypeResolveResult
        {
            public MethodDefinition method;
            public ParameterDefinition parameter;
            public ParameterTypeResolveResult(MethodDefinition m, ParameterDefinition p) { method = m; parameter = p; }
        }
        public class LocalVarTypeResolveResult : ITypeResolveResult
        {
            public VariableDefinition variable;
            public LocalVarTypeResolveResult(VariableDefinition v) { variable = v; }
        }
        public class InstructionTypeResolveResult : ITypeResolveResult
        {
            public MethodDefinition method;
            public Instruction instruction;
            public InstructionTypeResolveResult(MethodDefinition m, Instruction ins) { method = m; instruction = ins; }
        }
        public class BaseTypeTypeResolveResult : ITypeResolveResult
        {
            public TypeDefinition type;
            public BaseTypeTypeResolveResult(TypeDefinition t) { type = t; }
        }
        public class InterfaceTypeResolveResult : ITypeResolveResult
        {
            public TypeDefinition type;
            public InterfaceTypeResolveResult(TypeDefinition t) { type = t; }
        }


        public List<AssemblyDefinition> AssemblyList { get; private set; }
        public List<ModuleDefinition> ModuleList { get; private set; }
        public List<TypeDefinition> TypeList { get; private set; }

        public ReferenceResolver()
        {
            TypeList = new List<TypeDefinition>();
            ModuleList = new List<ModuleDefinition>();
            AssemblyList = new List<AssemblyDefinition>();
        }

        public void ScanAssembly(AssemblyDefinition assembly)
        {
            AssemblyList.Add(assembly);
            foreach (var module in assembly.Modules)
                ScanModule(module);
        }

        public void ScanModule(ModuleDefinition module)
        {
            ModuleList.Add(module);
            foreach (var type in module.Types)
                ScanType(type);
        }

        public void ScanType(TypeDefinition type)
        {
            TypeList.Add(type);
        }

        public List<InstructionResolveResult> FindAllReferences(MemberReference target)
        {
            return FindAllReferencesInInstructions(target);
        }

        public List<ITypeResolveResult> FindAllReferences(TypeReference target)
        {
            return FindAllReferencesEveryWhere(target);
        }

        public void ReplaceAllReferences(MemberReference origin, MemberReference alternative)
        {
            foreach (var reference in FindAllReferences(origin))
            {
                reference.instruction.Operand = alternative;
            }
        }

        public void ReplaceAllReferences(TypeReference origin, TypeReference alternative)
        {
            foreach (var reference in FindAllReferences(origin))
            {
                if (reference is FieldTypeResolveResult)
                {
                    var r = reference as FieldTypeResolveResult;
                    r.field.FieldType = alternative;
                }
                else if (reference is ReturnTypeTypeResolveResult)
                {
                    var r = reference as ReturnTypeTypeResolveResult;
                    r.method.ReturnType = alternative;
                }
                else if (reference is ParameterTypeResolveResult)
                {
                    var r = reference as ParameterTypeResolveResult;
                    r.parameter.ParameterType = alternative;
                }
                else if (reference is LocalVarTypeResolveResult)
                {
                    var r = reference as LocalVarTypeResolveResult;
                    r.variable.VariableType = alternative;
                }
                else if (reference is InstructionTypeResolveResult)
                {
                    var r = reference as InstructionTypeResolveResult;
                    r.instruction.Operand = alternative;
                }
                else
                {
                    throw new ArgumentException("Unknown resolve type.");
                }
            }
        }

        List<InstructionResolveResult> FindAllReferencesInInstructions(IMetadataTokenProvider target)
        {
            var result = new List<InstructionResolveResult>();
            foreach (var type in TypeList)
            {
                foreach (var method in type.Methods)
                {
                    foreach (var ins in method.Body.Instructions)
                    {
                        if (ReferenceEquals(target, ins.Operand))
                        {
                            result.Add(new InstructionResolveResult(method, ins));
                        }
                    }
                }
            }
            return result;
        }

        List<ITypeResolveResult> FindAllReferencesEveryWhere(IMetadataTokenProvider target)
        {
            var result = new List<ITypeResolveResult>();
            foreach (var type in TypeList)
            {
                if (ReferenceEquals(type.BaseType, target))
                    result.Add(new BaseTypeTypeResolveResult(type));
                foreach (var @interface in type.Interfaces)
                    if (ReferenceEquals(@interface.InterfaceType, target))
                        result.Add(new InterfaceTypeResolveResult(type));
                foreach (var method in type.Methods)
                {
                    if (ReferenceEquals(target, method.ReturnType))
                    {
                        result.Add(new ReturnTypeTypeResolveResult(method));
                    }

                    foreach (var param in method.Parameters)
                    {
                        if (ReferenceEquals(target, param))
                        {
                            result.Add(new ParameterTypeResolveResult(method, param));
                        }
                    }

                    foreach (var ins in method.Body.Instructions)
                    {
                        if (ReferenceEquals(target, ins.Operand))
                        {
                            result.Add(new InstructionTypeResolveResult(method, ins));
                        }
                    }

                    foreach (var local in method.Body.Variables)
                    {
                        if (ReferenceEquals(target, local))
                        {
                            result.Add(new LocalVarTypeResolveResult(local));
                        }
                    }
                }

                foreach (var field in type.Fields)
                {
                    if (ReferenceEquals(target, field.FieldType))
                    {
                        result.Add(new FieldTypeResolveResult(field));
                    }
                }
            }
            return result;
        }
    }
}
