using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BeaverBuddies
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

        public static void FindStaticFields()
        {
            string targetNamespace = "TimberModTest";
            IEnumerable<Type> typesInNamespace = GetAllTypes();
            Plugin.Log("Types: " + typesInNamespace.Count());
            typesInNamespace = typesInNamespace
                .Where(type => type.Namespace == targetNamespace);
            Plugin.Log("In Namespace: " + typesInNamespace.Count());

            foreach (var type in typesInNamespace)
            {
                // Check if the type implements IResettableSingleton
                if (typeof(IResettableSingleton).IsAssignableFrom(type))
                {
                    // Skip types that implement IResettableSingleton
                    continue;
                }

                // Get static fields for the current type
                var staticFields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                        .Where(field => !field.IsLiteral);

                foreach (var field in staticFields)
                {
                    // Print or process each static field
                    Plugin.Log($"Type: {type.Name}, Static Field: {field.Name}");
                }
            }
        }

        private static IEnumerable<Type> GetAllTypes()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            // Get all types in the target namespace
            var typesInNamespace = assemblies.SelectMany(a => a.GetTypes());
            return typesInNamespace;
        }

        public static void FindHashSetFields()
        {
            // Get all types in the assembly
            var types = GetAllTypes();

            foreach (var type in types)
            {
                // Only consider classes
                if (!type.IsClass)
                    continue;

                // Get all fields of the class
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var field in fields)
                {
                    // Check if the field is of type HashSet<>
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(HashSet<>))
                    {
                        Console.WriteLine($"Class: {type.FullName}, Field: {field.Name}");
                    }
                }
            }
        }
    }
}
