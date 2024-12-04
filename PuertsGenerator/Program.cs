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
using System.Text.RegularExpressions;

#nullable disable

class Program
{

    static void TryAddGenType(TypeDefinition type, List<TypeDefinition> types, HashSet<string> whitelist, HashSet<string> blacklist)
    {
        var typeKey = type.FullName;
        if (whitelist != null)
        {
            // 存在白名单就必须白名单有
            if (!whitelist.Contains(typeKey))
            {
                return;
            }
        }

        if (blacklist != null)
        {
            // 存在黑名单，就必须黑名单没有
            if (blacklist.Contains(typeKey))
            {
                return;
            }
        }

        if (type.HasNestedTypes)
        {
            foreach (var nt in type.NestedTypes)
            {
                if (nt.IsNestedPublic && !GenerateInfoCollector.isCompilerGenerated(nt))
                    TryAddGenType(nt, types, whitelist, blacklist);
            }
        }

        types.Add(type);
    }

    static void Main(string[] args)
    {
        string jsonString = File.ReadAllText(args[0]);
        GenerateConfigure conf = JsonSerializer.Deserialize<GenerateConfigure>(jsonString);

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

            List<string> searchDirectors = new List<string>();

            foreach (var arg in args.Skip(2))
            {
                searchDirectors.Add(Path.GetDirectoryName(Path.GetFullPath(arg)));
            }

            foreach (var arg in args.Skip(2))
            {
                try
                {
                    stopwatch.Start();
                    var assembly = AssemblyDefinition.ReadAssembly(arg);
                    stopwatch.Stop();
                    Console.WriteLine($"Read {assembly.Name.Name}({arg}) using: {stopwatch.ElapsedMilliseconds} ms");
                    AssemblyConfigure assemblyConfigure = conf.Assemblys.FirstOrDefault(x => new Regex(x.Key).IsMatch(assembly.Name.Name)).Value;
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
                        var baseResolver = module.AssemblyResolver as BaseAssemblyResolver;
                        if (baseResolver != null)
                        {
                            foreach (var dir in searchDirectors)
                            {
                                baseResolver.AddSearchDirectory(dir);
                            }
                        }
                        foreach (var type in module.Types)
                        {
                            if (Path.GetFileName(arg) == "mscorlib.dll")
                            {
                                if (type.Name == "Type" || type.Name == "Array")
                                {
                                    TryAddGenType(type, typesToGen, null, null);
                                    continue;
                                }
                            }

                            if ((type.IsPublic) && !GenerateInfoCollector.isCompilerGenerated(type) && !type.Name.StartsWith("<"))
                            {
                                TryAddGenType(type, typesToGen, whitelist, blacklist);
                            }
                        }
                    }
                }
                catch { }
            }

            if (conf.EnumGenerateHooks != null)
            {
                GenerateInfoCollector.enumGenerateHooks = conf.EnumGenerateHooks;
            }
            var data = GenerateInfoCollector.Collect(typesToGen, conf.CollectAllReferences);
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
