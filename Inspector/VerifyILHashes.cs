
using BeaverBuddies;
using HarmonyLib;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Inspector
{
    public class VerifyILHashes
    {
        public void Main()
        {
            //foreach (var type in typeof(Plugin).Assembly.GetTypes())
            //{
            //    var overwrite = type.GetCustomAttribute<ManualMethodOverwrite>();
            //    if (overwrite == null) continue;

            //    // `type` is the class the attribute was applied to
            //    Console.WriteLine($"Found overwrite on class: {type.FullName}");
            //    VerifyMethod(overwrite, type);
            //}

            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\Timberborn\Timberborn_Data\Managed\";
            string file = dir + "Timberborn.BlockObjectTools.dll";
            var source = DecompileMethod(file, "Timberborn.BlockObjectTools.BlockObjectTool", "Enter");
            Console.WriteLine(source);

        }

        void VerifyMethod(ManualMethodOverwrite attribute, Type type)
        {
            var (methodName, declaringType) = GetTargetMethod(attribute, type);
            Console.WriteLine($"  Target method: {declaringType.FullName}.{methodName}");
            var il = DecompileMethod(declaringType, methodName);
            Console.WriteLine($"Code: \n{il}");
        }

        (string methodName, Type declaringType) GetTargetMethod(ManualMethodOverwrite attribute, Type patchType)
        {
            var harmonyPatch = patchType.GetCustomAttributes(typeof(HarmonyPatch), true)
                .FirstOrDefault() as HarmonyPatch;
            if (harmonyPatch != null)
            {
                var methodInfo = harmonyPatch.info;
                return (methodInfo.methodName, methodInfo.declaringType);
            }

            // TODO: Add these properties to the ManualMethodOverwrite attribute instead
            throw new InvalidOperationException($"No HarmonyPatch attribute found on type {patchType.FullName}");
        }



        string DecompileMethod(Type type, string methodName)
        {
            var asmPath = type.Assembly.Location;
            return DecompileMethod(asmPath, type.FullName, methodName);
        }

        string DecompileMethod(string asmPath, string typeFullName, string methodName)
        {
            var module = new PEFile(asmPath);
            var compilation = new SimpleCompilation(module);
            var ilType = compilation.FindType(new FullTypeName(typeFullName)).GetDefinition();
            // Could be overloaded - just get the first for now
            var method = ilType.GetMethods().Where(m => m.Name == methodName).First();
            var decompiler = new CSharpDecompiler(asmPath, new DecompilerSettings());
            return decompiler.DecompileAsString(method.MetadataToken);
        }
    }

}
