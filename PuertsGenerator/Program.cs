using System.Collections;
using Nustache.Core;
using Nustache.Compilation;
using Mono.Cecil;
using System.Reflection;
using System.Xml.Linq;
using System.Diagnostics;
using System.Text;
using PuertsGenerator;
using Puerts;
using System.Linq;
using System.ComponentModel;

#nullable disable

class Program
{
    static bool isCompilerGenerated(TypeReference type)
    {
        if (type.IsGenericInstance)
        {
            return isCompilerGenerated((type as GenericInstanceType)!.ElementType);
        }
        var td = type as TypeDefinition;
        if (td != null && td.IsNested)
        {
            if (isCompilerGenerated(td.DeclaringType))
            {
                return true;
            }
        }
        return td != null && !td.IsInterface && td
            .CustomAttributes
            .Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }

    static bool isCompilerGenerated(MethodReference method)
    {
        if (method != null && method.IsGenericInstance)
        {
            return isCompilerGenerated((method as GenericInstanceMethod)!.ElementMethod);
        }
        var md = method as MethodDefinition;
        return md != null && md.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }

    static bool isCompilerGenerated(FieldReference field)
    {
        var fd = field as FieldDefinition;
        return fd != null && fd.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }

    static void FormatDateTime(RenderContext context, IList<object> arguments, IDictionary<string, object> options, RenderBlock fn, RenderBlock inverse)
    {
        if (arguments != null && arguments.Count > 0 && arguments[0] != null && arguments[0] is DateTime)
        {
            DateTime datetime = (DateTime)arguments[0];
            if (options != null && options.ContainsKey("format"))
                context.Write(datetime.ToString(options["format"] as string));
            else
                context.Write(datetime.ToString());
        }
    }

    class PropertyInfoCollected
    {
        public string Name;

        public bool IsStatic;

        public bool IsReadOnly;

        public TypeInfoCollected PropertyType;

        public string[] DocumentLines;
    }

    class ParameterInfoCollected
    {
        public string Name;

        public TypeInfoCollected ParameterType;

        public bool IsFirst;

        public bool IsLast;
    }

    class MethodInfoCollected
    {
        public string Name;

        public bool IsStatic;

        public TypeInfoCollected ReturnType;

        public ParameterInfoCollected[] Parameters;

        public string[] DocumentLines;
    }

    static MethodInfoCollected[] EmptyMethodInfos = new MethodInfoCollected[0];

    static PropertyInfoCollected[] EmptyPropertyInfos = new PropertyInfoCollected[0];

    class TypeInfoCollected
    {
        public string Name;
        public string Namespace;
        public string FullName;
        public string TypeScriptName;

        public TypeInfoCollected BaseType;

        public MethodInfoCollected[] Methods = EmptyMethodInfos;

        public PropertyInfoCollected[] Properties = EmptyPropertyInfos;

        public bool IsEnum = false;

        public string EnumKeyValues;

        public string Implements;

        public bool WithImplements = false;

        public bool Proceed = false;

        public bool HasGenericParameters;

        public TypeInfoCollected[] GenericParameters;

        public string[] DocumentLines;

        public bool IsFirst = false;

        public bool IsLast = false;
    }

    class NamespaceInfoCollected
    {
        public string Name;
        public TypeInfoCollected[] Types;
    }


    class GenCodeData
    {
        public NamespaceInfoCollected[] Namespaces;
    }

    struct TypeInfoCollectedKey
    {
        public string FullName;
        public string ModuleName;
    }

    static Dictionary<TypeInfoCollectedKey, TypeInfoCollected> fullnameToTypeInfo = new Dictionary<TypeInfoCollectedKey, TypeInfoCollected>();

    static ParameterInfoCollected CollectInfo(ParameterDefinition parameterDefinition)
    {
        return new ParameterInfoCollected()
        {
            Name = parameterDefinition.Name,
            ParameterType = CollectInfo(parameterDefinition.ParameterType),
            IsFirst = false,
            IsLast = false,
        };
    }

    static string[] ToLines(string doc)
    {
        if (doc == null)
        {
            return null;
        }
        return doc.Split('\n');
    }

    static List<TypeReference> typesRefed = new List<TypeReference>();
    static HashSet<TypeReference> typesRefedLookup = new HashSet<TypeReference>();

    static void AddRefedType(TypeReference typeReference)
    {
        if (typesRefedLookup.Contains(typeReference) || typeReference.Name.StartsWith("<"))
        {
            return;
        }
        typesRefedLookup.Add(typeReference);

        try
        {
            var typeDef = typeReference.Resolve();
            if (typeDef != null)
            {
                var baseType = typeDef.BaseType;
                if (baseType != null)
                {
                    AddRefedType(baseType);
                }
                foreach(var itf in typeDef.Interfaces)
                {
                    AddRefedType(itf.InterfaceType);
                }
            }
        }
        catch { }

        if (typeReference.IsGenericInstance)
        {
            foreach(var gt in (typeReference as GenericInstanceType).GenericArguments)
            {
                AddRefedType(gt);
            }
        }

        var rawType = Utils.GetRawType(typeReference);
        if (rawType.IsPointer || typeReference.IsPointer || rawType.IsGenericParameter)
        {
            return;
        }
        typesRefed.Add(rawType);

        //TODO: Delegate
    }

    static PropertyInfoCollected CollectInfo(FieldDefinition fieldDefinition)
    {
        if (!fieldDefinition.IsPublic)
        {
            return null; 
        }
        AddRefedType(fieldDefinition.FieldType);
        return new PropertyInfoCollected()
        {
            Name = fieldDefinition.Name,
            IsStatic = fieldDefinition.IsStatic,
            IsReadOnly = false,
            PropertyType = CollectInfo(fieldDefinition.FieldType),
            DocumentLines = ToLines(DocResolver.GetTsDocument(fieldDefinition)),
        };
    }

    static PropertyInfoCollected CollectInfo(PropertyDefinition propertyDefinition)
    {
        bool getterPublic = propertyDefinition.GetMethod != null ? propertyDefinition.GetMethod.IsPublic : false;
        bool setterPublic = propertyDefinition.SetMethod != null ? propertyDefinition.SetMethod.IsPublic : false;
        if (!getterPublic && !setterPublic)
        {
            return null;
        }
        bool IsStatic = propertyDefinition.GetMethod != null ? propertyDefinition.GetMethod.IsStatic : propertyDefinition.SetMethod.IsStatic;
        AddRefedType(propertyDefinition.PropertyType);
        return new PropertyInfoCollected()
        {
            Name = propertyDefinition.Name,
            IsStatic = IsStatic,
            IsReadOnly = propertyDefinition.SetMethod == null,
            PropertyType = CollectInfo(propertyDefinition.PropertyType),
            DocumentLines = ToLines(DocResolver.GetTsDocument(propertyDefinition))
        };
    }

    static MethodInfoCollected CollectInfo(MethodDefinition methodDefinition)
    {
        var parameters =  methodDefinition.Parameters.Select(CollectInfo).ToArray();
        if (parameters.Length > 0)
        {
            parameters[0].IsFirst = true;
            parameters[parameters.Length- 1].IsLast = true;
        }

        if (!methodDefinition.IsConstructor)
        {
            AddRefedType(methodDefinition.ReturnType);
        }

        foreach (var parameterType in methodDefinition.Parameters.Select(p => p.ParameterType))
        {
            AddRefedType(parameterType);
        }

        return new MethodInfoCollected()
        {
            Name = methodDefinition.IsConstructor ?  "constructor" :  methodDefinition.Name,
            IsStatic = methodDefinition.IsStatic,
            ReturnType = CollectInfo(methodDefinition.ReturnType),
            Parameters = parameters,
            DocumentLines = ToLines(DocResolver.GetTsDocument(methodDefinition)),
        };
    }

    static string[] EmptyDocumentLines = new string[0];

    static TypeInfoCollected CollectInfo(TypeReference typeReference)
    {
        TypeInfoCollected res;
        var key = new TypeInfoCollectedKey
        {
            FullName = typeReference.FullName,
            ModuleName = typeReference.Module.Name
        };
        if (!fullnameToTypeInfo.TryGetValue(key, out res))
        {
            res = new TypeInfoCollected()
            {
                Name = typeReference.Name.Replace('`', '$'),
                Namespace = typeReference.Namespace,
                FullName = typeReference.FullName,
                TypeScriptName = Utils.GetTypeScriptName(typeReference),
                DocumentLines = EmptyDocumentLines,
                HasGenericParameters = typeReference.HasGenericParameters,
                GenericParameters = typeReference.GenericParameters.Select(CollectInfo).ToArray()
            };
            if (res.GenericParameters.Length > 0)
            {
                res.GenericParameters[0].IsFirst = true;
                res.GenericParameters[res.GenericParameters.Length - 1].IsLast = true;
            }
            fullnameToTypeInfo[key] = res;

            try
            {
                var typeDef = typeReference.Resolve();
                if (typeDef != null)
                {
                    var baseType = typeDef.BaseType;
                    if (baseType != null)
                    {
                        res.BaseType = CollectInfo(baseType);
                    }
                }
            }
            catch { }
        }

        return res;
    }

    static TypeInfoCollected CollectInfo(TypeDefinition typeDefinition)
    {
        TypeInfoCollected res = CollectInfo(typeDefinition as TypeReference);

        if (res.Proceed) return res;

        res.DocumentLines = ToLines(DocResolver.GetTsDocument(typeDefinition));

        res.IsEnum = typeDefinition.IsEnum;
        if (res.IsEnum)
        {
            res.EnumKeyValues = string.Join(", ", typeDefinition.Fields.Where(f => f.Name != "value__" && f.IsPublic).Select(f => f.Name + " = " + f.Constant));
            res.Proceed = true;
            return res;
        }

        if (typeDefinition.BaseType != null)
        {
            res.BaseType = CollectInfo(typeDefinition.BaseType);
        }

        HashSet<string> names = new HashSet<string>();
        foreach (var p in typeDefinition.Properties)
        {
            if (p.GetMethod != null)
            {
                names.Add(p.GetMethod.Name);
            }
            if (p.SetMethod != null)
            {
                names.Add(p.SetMethod.Name);
            }
        }

        res.Methods = typeDefinition.Methods
            .Where(m => m.IsPublic && !(m.IsStatic && m.IsConstructor) && (!m.IsSpecialName || !names.Contains(m.Name)))
            .Select(CollectInfo).ToArray();

        res.Properties = typeDefinition.Properties
            .Select(CollectInfo)
            .Concat(typeDefinition.Fields.Select(CollectInfo))
            .Where(p => p != null)
            .ToArray();

        var interfaces = new List<TypeInfoCollected>();
        retrieveInterfacesOfClass(typeDefinition, interfaces);
        res.WithImplements = interfaces.Count > 0;
        res.Implements = res.WithImplements ? string.Join(", ", interfaces.Select(i => i.TypeScriptName).ToArray()) : "";

        return res;
    }

    static void retrieveInterfacesOfClass(TypeDefinition typeDefinition, List<TypeInfoCollected> infos)
    {
        infos.InsertRange(0, typeDefinition.Interfaces.Select(i => CollectInfo(i.InterfaceType)));
        try
        {
            if (typeDefinition.BaseType != null)
            {
                retrieveInterfacesOfClass(typeDefinition.BaseType.Resolve(), infos);
            }
        }
        catch { }
    }

    static void Main(string[] args)
    {
        var template1 = new Template();
        template1.Load(new StringReader(Templates.IndexDTs));
        var compiled = template1.Compile<GenCodeData>(null);

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
                                if (type.Name != "Type" && type.Name != "Array")
                                {
                                    continue;
                                }
                            }
                            if ((!type.HasGenericParameters || type.IsGenericInstance) && !isCompilerGenerated(type) && !type.Name.StartsWith("<"))
                            {
                                typesToGen.Add(type);
                            }
                        }
                    }
                }
                catch { }
            }

            var typeInfosToGen = typesToGen.Distinct().Select(CollectInfo).ToArray(); // force referenced types found
            var typesToGenLookup = typesToGen.Cast<TypeReference>().ToHashSet();

            var data = new GenCodeData
            {
                Namespaces = typesRefed.Where(t => !typesToGenLookup.Contains(t)).Select(CollectInfo).Concat(typeInfosToGen)
                    .GroupBy(ti => ti.Namespace)
                    .Select(g => new NamespaceInfoCollected()
                    {
                        Name = g.Key,
                        Types = g.ToArray()
                    })
                    .ToArray(),
            };
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
