using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Rocks;

#nullable disable

namespace PuertsGenerator
{
    internal class GenerateInfoCollector
    {
        internal static bool isCompilerGenerated(TypeReference type)
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

        internal static bool isCompilerGenerated(MethodReference method)
        {
            if (method != null && method.IsGenericInstance)
            {
                return isCompilerGenerated((method as GenericInstanceMethod)!.ElementMethod);
            }
            var md = method as MethodDefinition;
            return md != null && md.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }

        internal static bool isCompilerGenerated(FieldReference field)
        {
            var fd = field as FieldDefinition;
            return fd != null && fd.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }

        internal class PropertyInfoCollected
        {
            public string Name;

            public bool IsStatic;

            public bool AsMethod = false;

            public bool Getter = false;

            public bool Setter = false;

            public TypeInfoCollected PropertyType;

            public bool ContainsGenericParameter;

            public string[] DocumentLines;
        }

        internal class ParameterInfoCollected
        {
            public string Name;

            public TypeInfoCollected ParameterType;

            public bool IsFirst = false;

            public bool IsLast = false;
        }

        internal class MethodInfoCollected
        {
            public string Name;

            public bool IsStatic;

            public TypeInfoCollected ReturnType;

            public ParameterInfoCollected[] Parameters;

            public string[] DocumentLines;

            public bool WithPointerType;

            public bool IsConstructor;
        }

        static MethodInfoCollected[] EmptyMethodInfos = new MethodInfoCollected[0];

        static PropertyInfoCollected[] EmptyPropertyInfos = new PropertyInfoCollected[0];

        internal class GenericParameterInfoCollected
        {
            public string Name;
            public bool IsFirst = false;
            public bool IsLast = false;
        }

        internal class TypeInfoCollected
        {
            public string Name;
            public string Namespace;
            public string FullName;
            public string TypeScriptName;

            public TypeInfoCollected BaseType;

            public MethodInfoCollected[] Methods = EmptyMethodInfos;

            public PropertyInfoCollected[] Properties = EmptyPropertyInfos;

            public bool IsEnum = false;

            public bool IsInterface = false;

            public string ImplementsKeyword = "implements";

            public string DeclareKeyword;

            public string EnumKeyValues;

            public string Implements;

            public bool WithImplements = false;

            public bool Proceed = false;

            public bool HasGenericParameters;

            public GenericParameterInfoCollected[] GenericParameters;

            public string[] DocumentLines;
        }

        internal class NamespaceInfoCollected
        {
            public string Name;
            public TypeInfoCollected[] Types;
            public bool IsGlobal;
        }


        internal class GenCodeData
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
                    foreach (var itf in typeDef.Interfaces)
                    {
                        AddRefedType(itf.InterfaceType);
                    }
                }
            }
            catch { }

            if (typeReference.IsGenericInstance)
            {
                var genericInstanceType = (typeReference as GenericInstanceType);
                foreach (var gt in genericInstanceType.GenericArguments)
                {
                    AddRefedType(gt);
                }
                AddRefedType(genericInstanceType.ElementType);
                return;
            }
            if (typeReference.GenericParameters.Count > 0)
            {
                foreach (var gp in typeReference.GenericParameters)
                {
                    if (gp.Name.Contains('!'))
                    {
                        //AddRefedType(typeReference.GetElementType());
                        try
                        {
                            var typeDef = typeReference.Resolve();
                            if (typeDef != null) { AddRefedType(typeDef); }
                        } catch { }
                        return;
                    }
                }
            }

            if (typeReference.IsRequiredModifier)
            {
                AddRefedType(typeReference.GetElementType());
                return;
            }

            var rawType = Utils.GetRawType(typeReference);
            if (rawType.IsPointer || typeReference.IsPointer || rawType.IsGenericParameter)
            {
                return;
            }
            typesRefed.Add(rawType);

            //TODO: Delegate?
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
            var containsGenericParameter = propertyDefinition.ContainsGenericParameter;
            if (!containsGenericParameter)
            {
                if (propertyDefinition.GetMethod != null)
                {
                    containsGenericParameter = propertyDefinition.GetMethod.ContainsGenericParameter;
                }
                if (propertyDefinition.SetMethod != null)
                {
                    containsGenericParameter = propertyDefinition.SetMethod.ContainsGenericParameter;
                }
            }
            bool IsStatic = propertyDefinition.GetMethod != null ? propertyDefinition.GetMethod.IsStatic : propertyDefinition.SetMethod.IsStatic;
            AddRefedType(propertyDefinition.PropertyType);
            return new PropertyInfoCollected()
            {
                Name = propertyDefinition.Name,
                IsStatic = IsStatic,
                AsMethod = !propertyDefinition.DeclaringType.IsInterface,
                Getter = getterPublic,
                Setter = setterPublic,
                PropertyType = CollectInfo(propertyDefinition.PropertyType),
                ContainsGenericParameter = containsGenericParameter,
                DocumentLines = ToLines(DocResolver.GetTsDocument(propertyDefinition))
            };
        }

        static MethodInfoCollected CollectInfo(MethodDefinition methodDefinition)
        {
            var parameters = methodDefinition.Parameters.Select(CollectInfo).ToArray();
            if (parameters.Length > 0)
            {
                parameters[0].IsFirst = true;
                parameters[parameters.Length - 1].IsLast = true;
            }

            bool WithPointerType = false;

            if (!methodDefinition.IsConstructor)
            {
                AddRefedType(methodDefinition.ReturnType);
                if(methodDefinition.ReturnType.IsPointer)
                {
                    WithPointerType = true;
                }
            }

            foreach (var parameterType in methodDefinition.Parameters.Select(p => p.ParameterType))
            {
                AddRefedType(parameterType);
                if (parameterType.IsPointer)
                {
                    WithPointerType = true;
                }
            }

            return new MethodInfoCollected()
            {
                Name = methodDefinition.IsConstructor ? "constructor" : methodDefinition.Name,
                IsStatic = methodDefinition.IsStatic,
                ReturnType = CollectInfo(methodDefinition.ReturnType),
                Parameters = parameters,
                WithPointerType = WithPointerType,
                IsConstructor = methodDefinition.IsConstructor,
                DocumentLines = ToLines(DocResolver.GetTsDocument(methodDefinition)),
            };
        }

        static string[] EmptyDocumentLines = new string[0];

        static TypeInfoCollected CollectInfo(TypeReference typeReference)
        {
            TypeInfoCollected res;
            if (typeReference.IsRequiredModifier)
            {
                typeReference = typeReference.GetElementType();
            }
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
                    FullName = typeReference.FullName.Replace('`', '$').Replace('/', '.'),
                    TypeScriptName = Utils.GetTypeScriptName(typeReference),
                    DocumentLines = EmptyDocumentLines,
                    HasGenericParameters = typeReference.HasGenericParameters,
                    GenericParameters = typeReference.GenericParameters.Select(gp =>new GenericParameterInfoCollected { Name = gp.Name}).ToArray(),
                    DeclareKeyword = "class"
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
                        res.IsInterface = typeDef.IsInterface;
                        if (res.IsInterface)
                        {
                            res.DeclareKeyword = "interface";
                            res.ImplementsKeyword = "extends";
                        }
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

            if (typeDefinition.IsNested)
            {
                var nsp = new List<string>();
                var temp = typeDefinition;
                while (temp.IsNested)
                {
                    nsp.Add(temp.DeclaringType.Name.Replace('`', '$'));
                    temp = temp.DeclaringType;
                }
                nsp.Reverse();
                if (!string.IsNullOrEmpty(temp.Namespace))
                {
                    res.Namespace = temp.Namespace + '.' + string.Join(".", nsp.ToArray());
                }
                else
                {
                    res.Namespace = string.Join(".", nsp.ToArray());
                }
            }

            res.IsEnum = typeDefinition.IsEnum;
            if (res.IsEnum)
            {
                res.EnumKeyValues = string.Join(", ", typeDefinition.Fields.Where(f => f.Name != "value__" && f.IsPublic).Select(f => f.Name + " = " + f.Constant));
                res.Proceed = true;
                res.DeclareKeyword = "enum";
                return res;
            }

            res.IsInterface = typeDefinition.IsInterface;
            if (res.IsInterface)
            {
                res.ImplementsKeyword = "extends";
            }

            if (typeDefinition.BaseType != null)
            {
                AddRefedType(typeDefinition.BaseType);
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

            List<MethodDefinition> mustAdd = new List<MethodDefinition>();
            findSameNameButNotOverride(typeDefinition.Methods.Where(m => !m.IsConstructor).GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.Cast<MethodDefinition>()), typeDefinition.IsAbstract, typeDefinition, mustAdd, false);

            //foreach (var ma in mustAdd)
            {
            //    Console.WriteLine($"{typeDefinition} >>> ${ma}");
            }

            res.Methods = typeDefinition.Methods
                .Where(m => m.IsPublic && !(m.IsStatic && m.IsConstructor) && (!m.IsSpecialName || !names.Contains(m.Name)) && !m.HasGenericParameters)
                .Where(m => !m.ContainsGenericParameter || !m.IsStatic)
                .Concat(mustAdd)
                .Select(CollectInfo)
                .Where(mi => !mi.WithPointerType)
                .ToArray();

            res.Properties = typeDefinition.Properties
                .Where(p => !p.PropertyType.IsPointer)
                .DistinctBy(p => p.Name)
                .Select(CollectInfo)
                .Where(p => p != null)
                .Where(pi => !pi.ContainsGenericParameter || !pi.IsStatic)
                .Concat(typeDefinition.Fields.Where(f => !f.FieldType.IsPointer && (!f.ContainsGenericParameter || !f.IsStatic)).Select(CollectInfo))
                .Where(p => p != null)
                .ToArray();

            var interfaces = new List<TypeInfoCollected>();
            retrieveInterfacesOfClass(typeDefinition, interfaces);
            res.WithImplements = interfaces.Count > 0;
            res.Implements = res.WithImplements ? string.Join(", ", interfaces.Select(i => i.TypeScriptName).ToArray()) : "";

            return res;
        }

        static bool isOverride(MethodDefinition method, MethodDefinition baseOrNot)
        {
            var b = method.GetBaseMethod();
            if ( b == null || b == method)
            {
                return false;
            }
            return (b == baseOrNot) ? true : isOverride(b, baseOrNot);
        }

        static void findSameNameButNotOverride(Dictionary<string, IEnumerable<MethodDefinition>> methodMap, bool checkTypeIsAbstract, TypeDefinition baseType, List<MethodDefinition> result, bool findThisType)
        {
            if (findThisType)
            {
                foreach (var m in baseType.Methods)
                {
                    if (methodMap.TryGetValue(m.Name, out IEnumerable<MethodDefinition> methods))
                    {
                        bool found = false;
                        foreach (var m2 in methods)
                        {
                            if (isOverride(m2, m))
                            {
                                found = true;
                            }
                        }
                        if (!found) result.Add(m);
                    }
                    else
                    {
                        result.Add(m);
                    }
                }
            }
            //if (baseType.BaseType != null)
            //{
            //    var td = baseType.BaseType.Resolve();
            //    if (td != null)
            //    {
            //        findSameNameButNotOverride(methodMap, checkTypeIsAbstract, td, result, true);
            //    }
            //}
            if (baseType.IsInterface || checkTypeIsAbstract)
            {
                foreach (var itf in baseType.Interfaces)
                {
                    var td = itf.InterfaceType.Resolve();
                    if (td != null)
                    {
                        findSameNameButNotOverride(methodMap, checkTypeIsAbstract, td, result, true);
                    }
                }
            }
        }

        static void retrieveMethodToOverride(TypeDefinition typeDefinition)
        {
            ///typeDefinition.Methods[0].GetBaseMethod
        }

        static void retrieveInterfacesOfClass(TypeDefinition typeDefinition, List<TypeInfoCollected> infos)
        {
            foreach(var itf in typeDefinition.Interfaces)
            {
                AddRefedType(itf.InterfaceType);
            }
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

        internal static GenCodeData Collect(IEnumerable<TypeDefinition> typesToGen)
        {
            var typeInfosToGen = typesToGen.Distinct().Select(CollectInfo).ToArray(); // force referenced types found
            var typesToGenLookup = typesToGen.Select(t => t.FullName).ToHashSet();

            return new GenCodeData
            {
                Namespaces = typesRefed.Where(t => !typesToGenLookup.Contains(t.FullName)).DistinctBy((t) => t.FullName).Select(CollectInfo).Concat(typeInfosToGen)
                    .GroupBy(ti => ti.Namespace)
                    .Select(g => new NamespaceInfoCollected()
                    {
                        Name = g.Key,
                        Types = g.ToArray(),
                        IsGlobal = string.IsNullOrEmpty(g.Key),
                    })
                    .ToArray(),
            };
        }
    }
}
