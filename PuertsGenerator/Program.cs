using System.Collections;
using Nustache.Core;
using Nustache.Compilation;
using Mono.Cecil;
using System.Reflection;
using System.Xml.Linq;
using System.Diagnostics;
using System.Text;
using PuertsGenerator;
using System.Linq;
using System.Text.Json;

#nullable disable

class Program
{

    static void AddGenType(TypeDefinition type, List<TypeDefinition> types)
    {
        if (type.HasNestedTypes)
        {
            foreach (var nt in type.NestedTypes)
            {
                if (nt.IsNestedPublic && !GenerateInfoCollector.isCompilerGenerated(nt)) AddGenType(nt, types);
            }
        }

        types.Add(type);
    }

    static void Main(string[] args)
    {
        string jsonString = File.ReadAllText(args[0]);
        Dictionary<string, AssemblyConfigure> conf = JsonSerializer.Deserialize<Dictionary<string, AssemblyConfigure>>(jsonString);

        var output = args[1];

        /*
        foreach (var kvp in conf)
        {
            Console.WriteLine(kvp.Key);
            AssemblyConfigure assemblyConfigure = kvp.Value;
            if (assemblyConfigure.Whitelist != null)
            {
                Console.WriteLine("+++Whitelist");
                for (int i = 0; i < assemblyConfigure.Whitelist.Length; i++)
                {
                    Console.WriteLine(assemblyConfigure.Whitelist[i]);
                }
            }
            if (assemblyConfigure.Blacklist != null)
            {
                Console.WriteLine("---Blacklist");
                for (int i = 0; i < assemblyConfigure.Blacklist.Length; i++)
                {
                    Console.WriteLine($"{assemblyConfigure.Blacklist[i]}");
                }
            }
        }
        */

        var dtsTemplate = new Template();
        dtsTemplate.Load(new StringReader(Templates.IndexDTs));
        var compiled = dtsTemplate.Compile<GenerateInfoCollector.GenCodeData>(null);

        try
        {
            List<TypeDefinition> typesToGen = new List<TypeDefinition>();

            Stopwatch stopwatch = new Stopwatch();

            foreach (var arg in args.Skip(2))
            {
                try
                {
                    stopwatch.Start();
                    var assembly = AssemblyDefinition.ReadAssembly(arg);
                    stopwatch.Stop();
                    Console.WriteLine($"Read {assembly.Name.Name}({arg}) using: {stopwatch.ElapsedMilliseconds} ms");
                    AssemblyConfigure assemblyConfigure = conf[assembly.Name.Name];
                    HashSet<string> whitelist = null;
                    HashSet<string> blacklist = null;
                    if (assemblyConfigure != null && assemblyConfigure.Whitelist != null)
                    {
                        whitelist = assemblyConfigure.Whitelist.ToHashSet();
                    }
                    if (assemblyConfigure != null && assemblyConfigure.Blacklist != null)
                    {
                        blacklist = assemblyConfigure.Blacklist.ToHashSet();
                    }
                    stopwatch.Start();
                    foreach (var module in assembly.Modules)
                    {
                        foreach (var type in module.Types)
                        {
                            if (Path.GetFileName(arg) == "mscorlib.dll")
                            {
                                if (type.Name == "Type" || type.Name == "Array")
                                {
                                    AddGenType(type, typesToGen);
                                    continue;
                                }
                            }
                            var typeKey = $"{type.Namespace}.{type.Name}";
                            if (whitelist != null)
                            {
                                // 存在白名单就必须白名单有
                                if (!whitelist.Contains(typeKey))
                                {
                                    continue;
                                }
                            }

                            if (blacklist != null)
                            {
                                // 存在黑名单，就必须黑名单没有
                                if (blacklist.Contains(typeKey))
                                {
                                    continue;
                                }
                            }

                            if ((type.IsPublic) && !GenerateInfoCollector.isCompilerGenerated(type) && !type.Name.StartsWith("<"))
                            {
                                AddGenType(type, typesToGen);
                            }
                        }
                    }
                }
                catch { }
            }

            var data = GenerateInfoCollector.Collect(typesToGen);
            stopwatch.Stop();
            Console.WriteLine($"Data Prepare using: {stopwatch.ElapsedMilliseconds} ms, type.Count = {typesToGen.Count}");

            stopwatch.Start();
            string dts = compiled(data);
            stopwatch.Stop();
            Console.WriteLine($"Gen Code using: {stopwatch.ElapsedMilliseconds} ms, type.Count = {typesToGen.Count}");
            //private void FillPropertyGroup(ref EditorCurveBinding?[] propertyGroup, EditorCurveBinding lastBinding, string propertyGroupName, ref List<AnimationWindowCurve> curvesCache)
            using (StreamWriter textWriter = new StreamWriter(output, false, Encoding.UTF8))
            {
                textWriter.WriteLine(dts);
                //textWriter.WriteLine(Render.StringToString(Templates.IndexDTs, data));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"error {ex}");
        }
    }
}
