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

#nullable disable

class Program
{
    

    static void Main(string[] args)
    {
        var template1 = new Template();
        template1.Load(new StringReader(Templates.IndexDTs));
        var compiled = template1.Compile<GenerateInfoCollector.GenCodeData>(null);

        try
        {
            List<TypeDefinition> typesToGen = new List<TypeDefinition>();

            Stopwatch stopwatch = new Stopwatch();

            foreach (var arg in args)
            {
                try
                {
                    stopwatch.Start();
                    var assembly = AssemblyDefinition.ReadAssembly(arg);
                    stopwatch.Stop();
                    Console.WriteLine($"Read  ${arg} using: {stopwatch.ElapsedMilliseconds} ms");

                    stopwatch.Start();
                    foreach (var module in assembly.Modules)
                    {
                        foreach (var type in module.Types)
                        {
                            if (Path.GetFileName(arg) == "mscorlib.dll")
                            {
                                if (type.Name == "Dictionary`2" || type.Name == "List`1")
                                {
                                    typesToGen.Add(type);
                                    continue;
                                }
                                if (type.Name != "Type" && type.Name != "Array")
                                {
                                    continue;
                                }
                            }
                            if ((!type.HasGenericParameters || type.IsGenericInstance) && !GenerateInfoCollector.isCompilerGenerated(type) && !type.Name.StartsWith("<"))
                            {
                                typesToGen.Add(type);
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
            using (StreamWriter textWriter = new StreamWriter("index.d.ts", false, Encoding.UTF8))
            {
                textWriter.WriteLine(dts);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"error {ex.Message}");
        }
    }
}
