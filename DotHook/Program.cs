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

            var targetClass = TypeInspector.GetDefinitionByType(typeof(Injected));
            var injectedClass = CodeInjector.InjectClass(targetAsm.MainModule, targetClass);
            injectedClass.Namespace = "Target";

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

        public class Injected
        {
            public Injected()
            {
                Console.WriteLine("class created!");
            }
        }
    }
}
