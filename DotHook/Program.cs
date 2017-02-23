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
            Func<int, int, int> hook = (new Program()).Hook;
            var injectingMethod = TypeInspector.GetDefinitionByMethodInfo(hook.Method);
            ILInjector.HookBeforeCallReturn(targetMethod, injectingMethod);

            
            targetAsm.Write(File.OpenWrite("Target.Hooked.exe"));
        }

        int Hook(int b, int oldReturn)
        {
            var self = (Target.Target)(object)this;
            Console.WriteLine("m_a={0}, b={1}, m_a+b={2}", self.m_a, b, oldReturn);
            return oldReturn;
        }
    }
}
