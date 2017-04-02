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
        /// <summary>
        /// Inject a clone of source method to target class.
        /// </summary>
        /// <param name="target class"></param>
        /// <param name="source method"></param>
        /// <param name="method name"></param>
        /// <returns>the newly injected method</returns>
        static public MethodDefinition InjectMethod(TypeDefinition targetClass, MethodDefinition sourceMethod, string methodName = null)
        {
            if (methodName == null)
                methodName = sourceMethod.Name;
            var newMethod = CloneMethod(sourceMethod);
            newMethod.Name = methodName;
            targetClass.Methods.Add(newMethod);
            newMethod.DeclaringType = targetClass;
            FixReferences(newMethod);
            return newMethod;
        }

        /// <summary>
        /// Inject a clone of source class to target module.
        /// </summary>
        /// <param name="target module"></param>
        /// <param name="source class"></param>
        /// <param name="class name"></param>
        /// <returns>the newly injected class</returns>
        static public TypeDefinition InjectClass(ModuleDefinition targetModule, TypeDefinition sourceClass, string className = null)
        {
            if (className == null)
                className = sourceClass.Name;
            var newClass = CloneClass(sourceClass);
            newClass.Name = className;

            var isPublic = IsClassPublic(sourceClass);
            newClass.IsNestedFamily = false;
            newClass.IsNestedPrivate = false;
            newClass.IsNestedPublic = false;
            newClass.IsPublic = isPublic;

            newClass.DeclaringType = null;
            targetModule.Types.Add(newClass);
            FixReferences(newClass);
            return newClass;
        }

        /// <summary>
        /// Inject a clone of source to target class. The injected class will be nested.
        /// </summary>
        /// <param name="target class"></param>
        /// <param name="source class"></param>
        /// <param name="class name"></param>
        /// <returns>the newly injected class</returns>
        static public TypeDefinition InjectClass(TypeDefinition targetClass, TypeDefinition sourceClass, string className = null)
        {
            if (className == null)
                className = sourceClass.Name;
            var newClass = CloneClass(sourceClass);
            newClass.Name = className;

            var isPublic = IsClassPublic(sourceClass);
            newClass.IsPublic = false;
            newClass.IsNestedPublic = isPublic;

            newClass.DeclaringType = targetClass;
            targetClass.NestedTypes.Add(newClass);
            FixReferences(newClass);
            return newClass;
        }

        /// <summary>
        /// Replace target method with your hook method.
        /// To call the original method, just call hook method from the hook method.
        /// </summary>
        /// <param name="target method"></param>
        /// <param name="hook method"></param>
        /// <param name="resolver">This is used to find all instructions that call the target method.</param>
        /// <param name="hook prefix">This will be used as the prefix of the original target method.</param>
        static public void HookMethod(MethodDefinition targetMethod, MethodDefinition hookMethod, ReferenceResolver resolver = null, string hookPrefix = "__hooked__")
        {
            if (resolver == null)
            {
                resolver = new ReferenceResolver();
                resolver.ScanAssembly(targetMethod.DeclaringType.Module.Assembly);
            }

            var injectedMethod = InjectMethod(targetMethod.DeclaringType, hookMethod);
            injectedMethod.Attributes = targetMethod.Attributes;
            if (!targetMethod.IsStatic && hookMethod.IsStatic)
            {
                // first argument is keyword "this"
                injectedMethod.HasThis = true;
                injectedMethod.Parameters.RemoveAt(0); // should not appear here
            }
            // rename original method
            injectedMethod.Name = targetMethod.Name;
            targetMethod.Name = hookPrefix + targetMethod.Name;
            // replace the original method with the new one
            resolver.ReplaceAllReferences(targetMethod, injectedMethod);
            // process self-calls
            for (int i = 0; i < injectedMethod.Body.Instructions.Count; ++i)
            {
                var ins = injectedMethod.Body.Instructions[i];
                if (ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt)
                {
                    if ((ins.Operand as MethodReference).FullName == hookMethod.FullName)
                    {
                        ins.Operand = targetMethod;
                    }
                }
            }
        }

        /// <summary>
        /// Hook a field by injecting a "getter" method.
        /// If target field is a static field, your getter should accept no arguments.
        /// If target field is not a static field, your getter show accept a object having the field.
        /// Return value of your getter method will be used instead of the original field value.
        /// </summary>
        /// <param name="target field"></param>
        /// <param name="hook method">your "getter" method.</param>
        /// <param name="resolver">This is used to find every IL instruction that reads the field.</param>
        /// <param name="hook name"></param>
        static public void HookFieldRead(FieldDefinition target, MethodDefinition hookMethod, ReferenceResolver resolver = null, string hookName = null)
        {
            if (hookName == null)
                hookName = target.Name + "__hookread__" + hookMethod.Name;

            if (resolver == null)
            {
                resolver = new ReferenceResolver();
                resolver.ScanAssembly(target.DeclaringType.Module.Assembly);
            }

            var injectedMethod = InjectMethod(target.DeclaringType, hookMethod, hookName);

            var references = resolver.FindAllReferences(target);
            foreach (var reference in references)
            {
                if (reference.instruction.OpCode == OpCodes.Ldfld || reference.instruction.OpCode == OpCodes.Ldsfld)
                {
                    reference.instruction.OpCode = OpCodes.Call;
                    reference.instruction.Operand = injectedMethod;
                }
            }
        }

        /// <summary>
        /// Hook a field by injecting a static "setter" method.
        /// If target field is a static field, your setter should accept a value that is originally used to write the field.
        /// If target field is not a static field, your setter should accept a object having the field followed by a value.
        /// Your setter method should not have any return value.
        /// </summary>
        /// <param name="target field"></param>
        /// <param name="hook method">your "setter"</param>
        /// <param name="resolver">This is used to find every IL instruction that writes the field.</param>
        /// <param name="hookName"></param>
        static public void HookFieldWrite(FieldDefinition target, MethodDefinition hookMethod, ReferenceResolver resolver = null, string hookName = null)
        {
            if (hookName == null)
                hookName = target.Name + "__hookwrite__" + hookMethod.Name;

            if (resolver == null)
            {
                resolver = new ReferenceResolver();
                resolver.ScanAssembly(target.DeclaringType.Module.Assembly);
            }

            var injectedMethod = InjectMethod(target.DeclaringType, hookMethod, hookName);

            var references = resolver.FindAllReferences(target);
            foreach (var reference in references)
            {
                if (reference.instruction.OpCode == OpCodes.Stfld || reference.instruction.OpCode == OpCodes.Stsfld)
                {
                    reference.instruction.OpCode = OpCodes.Call;
                    reference.instruction.Operand = injectedMethod;
                }
            }
        }

        static public TypeDefinition CloneClass(TypeDefinition source)
        {
            var newClass = new TypeDefinition(source.Namespace, source.Name, source.Attributes);

            newClass.BaseType = source.BaseType;

            foreach (var @interface in source.Interfaces)
                newClass.Interfaces.Add(@interface);

            foreach (var field in source.Fields)
            {
                var newField = new FieldDefinition(field.Name, field.Attributes, field.FieldType);
                newField.Constant = field.Constant;
                newField.InitialValue = field.InitialValue;
                newField.DeclaringType = newClass;
                newClass.Fields.Add(newField);
            }

            foreach (var method in source.Methods)
            {
                var newMethod = CloneMethod(method);
                newMethod.DeclaringType = newClass;
                newClass.Methods.Add(newMethod);
            }

            foreach (var @event in source.Events)
            {
                var newEvent = new EventDefinition(@event.Name, @event.Attributes, @event.EventType);
                newEvent.AddMethod = CloneMethod(@event.AddMethod);
                newEvent.RemoveMethod = CloneMethod(@event.RemoveMethod);
                foreach (var method in @event.OtherMethods)
                    newEvent.OtherMethods.Add(CloneMethod(method));
                newEvent.DeclaringType = newClass;
                newClass.Events.Add(newEvent);
            }

            foreach (var @class in source.NestedTypes)
            {
                var newNestedClass = CloneClass(@class);
                newNestedClass.DeclaringType = newClass;
                newClass.NestedTypes.Add(newNestedClass);
            }

            return newClass;
        }

        static public MethodDefinition CloneMethod(MethodDefinition source)
        {
            var newMethod = new MethodDefinition(source.Name, source.Attributes, source.ReturnType);
            newMethod.CallingConvention = source.CallingConvention;
            newMethod.HasThis = source.HasThis;

            foreach (var param in source.Parameters)
            {
                var newParam = new ParameterDefinition(param.Name, param.Attributes, param.ParameterType);
                newMethod.Parameters.Add(newParam);
            }
            foreach (var variable in source.Body.Variables)
            {
                var newVariable = new VariableDefinition(variable.VariableType);
                newMethod.Body.Variables.Add(newVariable);
            }
            var il = newMethod.Body.GetILProcessor();
            // copy all instructions
            foreach (var sourceIns in source.Body.Instructions)
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
            // copy all exception handlers
            foreach (var handler in source.Body.ExceptionHandlers)
            {
                var newHandler = new ExceptionHandler(handler.HandlerType);
                newHandler.CatchType = handler.CatchType;
                newHandler.FilterStart = handler.FilterStart;
                newHandler.HandlerStart = handler.HandlerStart;
                newHandler.HandlerEnd = handler.HandlerEnd;
                newHandler.TryStart = handler.TryStart;
                newHandler.TryEnd = handler.TryEnd;
                newMethod.Body.ExceptionHandlers.Add(newHandler);
            }
            return newMethod;
        }

        static public void FixReferences(MethodDefinition method)
        {
            var module = method.Module;

            method.ReturnType = module.ImportReference(method.ReturnType);

            for (int i = 0; i < method.Body.Instructions.Count; ++i)
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
            foreach (var handler in method.Body.ExceptionHandlers)
                handler.CatchType = module.ImportReference(handler.CatchType);
        }

        static public void FixReferences(TypeDefinition type)
        {
            var module = type.Module;
            if (module == null)
                throw new ArgumentException("This type is not attached to a module.");

            type.BaseType = module.ImportReference(type.BaseType);

            for (int i=0; i<type.Interfaces.Count; ++i)
            {
                var @interface = type.Interfaces[i];
                type.Interfaces[i] = new InterfaceImplementation(module.ImportReference(@interface.InterfaceType));
            }

            foreach (var field in type.Fields)
                field.FieldType = module.ImportReference(field.FieldType);

            foreach (var method in type.Methods)
                FixReferences(method);

            foreach (var nested in type.NestedTypes)
                FixReferences(nested);
        }

        static public bool IsClassPublic(TypeDefinition type)
        {
            if (type.IsNested)
                return type.IsNestedPublic;
            else
                return type.IsPublic;
        }
    }
}
