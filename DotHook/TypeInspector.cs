using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;


using Mono.Cecil;

namespace DotHook
{
    /// <summary>
    /// Find types in TypeList by its FullName.
    /// </summary>
    public class TypeInspector
    {
        public List<TypeDefinition> TypeList { get; private set; }
        public List<AssemblyDefinition> AssemblyList { get; private set; }
        private Dictionary<string, TypeDefinition> m_typeCache;

        public TypeInspector()
        {
            TypeList = new List<TypeDefinition>();
            AssemblyList = new List<AssemblyDefinition>();
            m_typeCache = new Dictionary<string, TypeDefinition>();
        }

        /// <summary>
        /// Add all types in an assembly into TypeList.
        /// </summary>
        /// <param name="assembly">assembly to scan</param>
        public void ScanAssembly(AssemblyDefinition assembly)
        {
            foreach (var module in assembly.Modules)
                ScanModule(module);
            AssemblyList.Add(assembly);
        }

        /// <summary>
        /// Add all types in an assembly into TypeList.
        /// </summary>
        /// <param name="AssemblyPath">assembly to scan</param>
        public void ScanAssembly(string AssemblyPath)
        {
            ScanAssembly(AssemblyDefinition.ReadAssembly(File.OpenRead(AssemblyPath)));
        }

        /// <summary>
        /// Add all types in a module into TypeList.
        /// </summary>
        /// <param name="module">module to scan</param>
        public void ScanModule(ModuleDefinition module)
        {
            TypeList.AddRange(module.Types);
        }

        /// <summary>
        /// Find a type in TypeList by its FullName
        /// </summary>
        /// <param name="path">full name of the type to find</param>
        /// <returns></returns>
        public TypeDefinition FindTypeByFullName(string path)
        {
            if (path.StartsWith("+"))
            {
                throw new ArgumentException("Path must not start with a +.");
            }
            if (path.EndsWith("+"))
            {
                path = path.Substring(0, path.Length - 1);
            }
            if (m_typeCache.ContainsKey(path))
            {
                return m_typeCache[path];
            }

            string[] components = path.Split('+');

            var firstTypeName = components[0];
            var cachePath = "+" + firstTypeName;

            TypeDefinition firstType;
            if (m_typeCache.ContainsKey(cachePath))
            {
                firstType = m_typeCache[cachePath];
            }
            else
            {
                firstType = TypeList.Single(t => t.FullName == firstTypeName);
                m_typeCache.Add(cachePath, firstType);
            }

            var type = firstType;
            for (int i = 1; i < components.Length; ++i)
            {
                var typeName = components[i];
                cachePath += "+" + typeName;
                if (m_typeCache.ContainsKey(cachePath))
                {
                    type = m_typeCache[cachePath];
                }
                else
                {
                    type = type.NestedTypes.Single(t => t.Name == typeName);
                    m_typeCache.Add(cachePath, type);
                }
            }

            return type;
        }

        /// <summary>
        /// Find a method by type path and method name.
        /// </summary>
        /// <param name="path">path</param>
        /// <param name="methodName">method name</param>
        /// <returns></returns>
        public MethodDefinition FindMethodByName(string path, string methodName)
        {
            return FindMethodsByName(path, methodName).Single();
        }

        /// <summary>
        /// Find all methods by type path and method name.
        /// </summary>
        /// <param name="path">path of the type declaring methods to find</param>
        /// <param name="methodName">name of methods to find</param>
        /// <returns></returns>
        public IEnumerable<MethodDefinition> FindMethodsByName(string path, string methodName)
        {
            var type = FindTypeByFullName(path);
            return type.Methods.Where(m => m.Name == methodName);
        }

        /// <summary>
        /// Find a method by path.
        /// </summary>
        /// <param name="path">path of the method</param>
        /// <returns></returns>
        public MethodDefinition FindMethodByName(string path)
        {
            return FindMethodsByName(path).Single();
        }

        /// <summary>
        /// Find all methods by path.
        /// </summary>
        /// <param name="path">path of methods</param>
        /// <returns></returns>
        public IEnumerable<MethodDefinition> FindMethodsByName(string path)
        {
            var split = path.LastIndexOf('.');
            return FindMethodsByName(path.Substring(0, split), path.Substring(split + 1));
        }

        public MethodDefinition[] FindMethodReg(string path, string methodNameReg)
        {
            var type = FindTypeByFullName(path);
            var methods = new List<MethodDefinition>();
            foreach (var method in type.Methods)
            {
                if (Regex.IsMatch(method.Name, methodNameReg))
                    methods.Add(method);
            }
            return methods.ToArray();
        }

        /// <summary>
        /// Get TypeDefinition from a runtime Type object.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static TypeDefinition GetDefinitionByType(Type type)
        {
            TypeInspector inspector = new TypeInspector();
            inspector.ScanAssembly(type.Assembly.Location);
            return inspector.FindTypeByFullName(type.FullName);
        }

        /// <summary>
        /// Get MethodDefinition from a runtime MethodInfo object.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static MethodDefinition GetDefinitionByMethodInfo(MethodInfo method)
        {
            Type type = method.DeclaringType;
            TypeDefinition typeDefinition = GetDefinitionByType(type);
            return typeDefinition.Methods.Single(m => {
                if(m.Name != method.Name)
                    return false;
                var targetParams = method.GetParameters();
                var currentParams = m.Parameters;
                if (targetParams.Length != currentParams.Count)
                    return false;
                for (var i=0;i<currentParams.Count;++i)
                {
                    var targetParam = targetParams[i];
                    var currentParam = currentParams[i];
                    if(targetParam.ParameterType.ToString()!=currentParam.ParameterType.ToString())
                        return false;
                }
                return true;
            });
        }
    }
}
