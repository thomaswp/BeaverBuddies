using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Metadata;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        string folderPath = @"C:\Program Files (x86)\Steam\steamapps\common\Timberborn\Timberborn_Data\Managed";
        HashSet<string> methodNames = new HashSet<string>(new string[] {
            "First",
            "FirstOrDefault",
            "Last",
            "LastOrDefault",
            "ElementAt",
        });

        foreach (string file in Directory.GetFiles(folderPath, "*.dll"))
        {
            string fileName = Path.GetFileName(file);
            if (!fileName.StartsWith("Timberborn")) continue;
            //if (fileName != "Timberborn.SlotSystem.dll") continue;
            //Console.WriteLine($"Processing {fileName}...");
            try
            {
                var module = new PEFile(file);
                var decompiler = new CSharpDecompiler(file, new DecompilerSettings());
                var syntaxTree = decompiler.DecompileWholeModuleAsSingleFile();
                var hashSetCreations = FindHashSetCreations(syntaxTree);
                if (hashSetCreations.Count == 0) continue;
                var methodCalls = FindMethodCalls(fileName, syntaxTree, methodNames);
                if (methodCalls.Count == 0) continue;

                Console.WriteLine(fileName);
                foreach (string c in hashSetCreations)
                {
                    Console.WriteLine(c);
                }
                foreach (string c in methodCalls)
                {
                    Console.WriteLine(c);
                }
                Console.WriteLine("\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to process {fileName}: {ex.Message}");
            }
        }
    }

    static List<string> FindHashSetCreations(SyntaxTree syntaxTree)
    {
        var results = new List<string>();

        foreach (var node in syntaxTree.Descendants)
        {
            if (node is ObjectCreateExpression objectCreate && objectCreate.Type.ToString().StartsWith("HashSet")) // && objectCreate.Type.ToString() == "HashSet")
            {
                //var type = objectCreate.Annotation<ITypeReference>().ToString();
                results.Add($"{objectCreate.Parent}");
            }
        }

        return results;
    }


    static List<string> FindMethodCalls(string fileName, SyntaxTree syntaxTree, HashSet<string> methodNames)
    {
        var results = new List<string>();
        foreach (var node in syntaxTree.Descendants)
        {
            if (node is InvocationExpression invocation && invocation.Target is MemberReferenceExpression memberReference)
            {
                if (methodNames.Contains(memberReference.MemberName))
                {
                    //Console.WriteLine(GetObjectType(invocation.Target));

                    var method = invocation.GetParent<MethodDeclaration>();
                    var type = method?.GetParent<TypeDeclaration>();

                    results.Add($"{type?.Name}.{method?.Name}:\n\t{invocation}");
                }
            }
        }
        return results;
    }

    static string GetObjectType(AstNode node)
    {
        if (node is IdentifierExpression identifier)
        {
            return identifier.Annotation<ITypeReference>().ToString();
        }
        else if (node is MemberReferenceExpression memberReference)
        {
            return GetObjectType(memberReference.Target);
        }
        else if (node is ObjectCreateExpression objectCreate)
        {
            return objectCreate.Annotation<ITypeReference>().ToString();
        }
        return "Unknown";
    }
}
