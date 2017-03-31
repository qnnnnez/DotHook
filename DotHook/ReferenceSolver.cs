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
        public interface IMetaResolveResult { }

        public class InstructionResolveResult : IMetaResolveResult
        {
            public MethodDefinition method;
            public Instruction instruction;
            public InstructionResolveResult(MethodDefinition m, Instruction ins) { method = m; instruction = ins; }
        }
        public class FieldResolveResult : IMetaResolveResult
        {
            public FieldDefinition field;
            public FieldResolveResult(FieldDefinition f) { field = f; }
        }
        public class ReturnTypeResolveResult : IMetaResolveResult
        {
            public MethodDefinition method;
            public ReturnTypeResolveResult(MethodDefinition m) { method = m; }
        }
        public class ParameterResolveResult : IMetaResolveResult
        {
            public MethodDefinition method;
            public ParameterDefinition parameter;
            public ParameterResolveResult(MethodDefinition m, ParameterDefinition p) { method = m; parameter = p; }
        }
        public class LocalVarResolveResult : IMetaResolveResult
        {
            public MethodDefinition method;
            public VariableDefinition variable;
            public LocalVarResolveResult(MethodDefinition m, VariableDefinition v) { variable = v; method = m; }
        }
        public class BaseTypeResolveResult : IMetaResolveResult
        {
            public TypeDefinition type;
            public BaseTypeResolveResult(TypeDefinition t) { type = t; }
        }
        public class InterfaceResolveResult : IMetaResolveResult
        {
            public TypeDefinition type;
            public InterfaceResolveResult(TypeDefinition t) { type = t; }
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

        public List<IMetaResolveResult> FindAllReferences(TypeReference target)
        {
            return FindAllReferencesEveryWhere(target);
        }

        public List<InterfaceResolveResult> FindAllReferences(InterfaceImplementation @interface)
        {
            return FindAllReferencesEveryWhere(@interface).Select(result => result as InterfaceResolveResult).ToList();
        }

        public void ReplaceAllReferences(MethodReference origin, MethodReference alternative, bool importing = true)
        {
            foreach (var reference in FindAllReferences(origin))
                reference.instruction.Operand = importing ? reference.method.Module.ImportReference(alternative) : alternative;
        }

        public void ReplaceAllReferences(FieldReference origin, FieldReference alternative, bool importing = true)
        {
            foreach (var reference in FindAllReferences(origin))
                reference.instruction.Operand = importing ? reference.method.Module.ImportReference(alternative) : alternative;
        }

        public void ReplaceAllReferences(TypeReference origin, TypeReference alternative, bool importing = true)
        {
            foreach (var reference in FindAllReferences(origin))
            {
                if (reference is FieldResolveResult)
                {
                    var r = reference as FieldResolveResult;
                    r.field.FieldType = importing ? r.field.Module.ImportReference(alternative) : alternative;
                }
                else if (reference is ReturnTypeResolveResult)
                {
                    var r = reference as ReturnTypeResolveResult;
                    r.method.ReturnType = importing ? r.method.Module.ImportReference(alternative) : alternative;
                }
                else if (reference is ParameterResolveResult)
                {
                    var r = reference as ParameterResolveResult;
                    r.parameter.ParameterType = importing ? r.method.Module.ImportReference(alternative) : alternative;
                }
                else if (reference is LocalVarResolveResult)
                {
                    var r = reference as LocalVarResolveResult;
                    r.variable.VariableType = importing ? r.method.Module.ImportReference(alternative) : alternative;
                }
                else if (reference is InstructionResolveResult)
                {
                    var r = reference as InstructionResolveResult;
                    r.instruction.Operand = importing ? r.method.Module.ImportReference(alternative) : alternative;
                }
                else if (reference is BaseTypeResolveResult)
                {
                    var r = reference as BaseTypeResolveResult;
                    r.type.BaseType = importing ? r.type.Module.ImportReference(alternative) : alternative;
                }
                else
                {
                    throw new ArgumentException("Wrong resolve type.");
                }
            }
        }

        public void ReplaceAllReferences(InterfaceImplementation origin, InterfaceImplementation alternative)
        {
            foreach (var result in FindAllReferences(origin))
            {
                int i = result.type.Interfaces.IndexOf(origin);
                result.type.Interfaces[i] = alternative;
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

        List<IMetaResolveResult> FindAllReferencesEveryWhere(IMetadataTokenProvider target)
        {
            var result = new List<IMetaResolveResult>();
            foreach (var type in TypeList)
            {
                if (ReferenceEquals(type.BaseType, target))
                    result.Add(new BaseTypeResolveResult(type));

                foreach (var @interface in type.Interfaces)
                    if (ReferenceEquals(target, @interface))
                        result.Add(new InterfaceResolveResult(type));

                foreach (var method in type.Methods)
                {
                    if (ReferenceEquals(target, method.ReturnType))
                        result.Add(new ReturnTypeResolveResult(method));

                    foreach (var param in method.Parameters)
                        if (ReferenceEquals(target, param))
                            result.Add(new ParameterResolveResult(method, param));

                    foreach (var ins in method.Body.Instructions)
                        if (ReferenceEquals(target, ins.Operand))
                            result.Add(new InstructionResolveResult(method, ins));

                    foreach (var local in method.Body.Variables)
                        if (ReferenceEquals(target, local))
                            result.Add(new LocalVarResolveResult(method, local));
                }

                foreach (var field in type.Fields)
                    if (ReferenceEquals(target, field.FieldType))
                        result.Add(new FieldResolveResult(field));
            }
            return result;
        }
    }
}
