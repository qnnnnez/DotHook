using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq.Expressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DotHook
{
    public class Program
    {
        static void Main(string[] args)
        {
            AssemblyDefinition targetAsm = AssemblyDefinition.ReadAssembly(File.OpenRead("Target.exe"));

            var inspector = new TypeInspector();
            inspector.ScanAssembly(targetAsm);
            inspector.ScanAssembly(typeof(Program).Assembly.Location);
            inspector.ScanAssembly(typeof(Console).Assembly.Location);

            var targetField = targetAsm.MainModule.Types.Single(t => t.Name == "Target").Fields.Single(f => f.Name == "m_a");
            CodeInjector.HookFieldRead(targetField, TypeInspector.GetDefinitionByMethodInfo(new Func<Target.Target, int>(HookRead).Method));
            CodeInjector.HookFieldWrite(targetField, TypeInspector.GetDefinitionByMethodInfo(new Action<Target.Target, int>(HookWrite).Method));

            targetAsm.Write(File.OpenWrite("Target.Hooked.exe"));
        }

        static int HookRead(Target.Target self)
        {
            Console.WriteLine("Reading a field.");
            return self.m_a;
        }

        static void HookWrite(Target.Target self, int value)
        {
            Console.WriteLine("Writing a field.");
            self.m_a = value;
        }
    }
}
