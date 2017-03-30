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

            var targetMethod = inspector.FindMethodByName("Target.Target", "Add");
            Func<int, int> hook = (new Program()).Hook;
            var injectingMethod = TypeInspector.GetDefinitionByMethodInfo(hook.Method);
            CodeInjector.HookMethod(targetMethod, injectingMethod);

            
            targetAsm.Write(File.OpenWrite("Target.Hooked.exe"));
        }

        int Hook(int b)
        {
            var self = (Target.Target)(object)this;
            Console.WriteLine("m_a={0}, b={1}", self.m_a, b);
            int result = Hook(b);
            Console.WriteLine("Result={0}", result);
            return result;
        }
    }
}
