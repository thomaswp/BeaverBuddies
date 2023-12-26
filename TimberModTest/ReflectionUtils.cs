using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace TimberModTest
{
    public static class ReflectionUtils
    {
        public static void PrintChildClasses(Type baseType, params string[] methodsOfInterest)
        {
            List<string> methods = new List<string>(methodsOfInterest);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var derivedTypes = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                // Filter out only the classes
                .Where(type => type.IsClass)
                // Filter out only the types that inherit from the specified base class
                .Where(type => baseType.IsAssignableFrom(type) && type != baseType)
                //.Where(type => type.GetMethods().Any(method => methods.Contains(method.Name)))
                .OrderBy(type => type.FullName);
                ;

            

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Classes inhereting: {baseType.Name}");
            foreach (var type in derivedTypes)
            {
                var includedMethods = methods.Where(method => type.GetMethods().Any(m => m.Name == method));
                if (methods.Count > 0 && includedMethods.Count() == 0) continue;
                string methodsIncluded = string.Join(", ", includedMethods);
                sb.AppendLine($"{type.FullName}: {methodsIncluded}");
            }
            sb.AppendLine();
            Plugin.Log(sb.ToString());
        }
    }
}
