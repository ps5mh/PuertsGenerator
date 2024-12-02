using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System.Text.RegularExpressions;
using Nustache.Core;

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

            public string ParamerterTypeScriptName; // 因为TypeInfoCollected是根据类型索引的，可能params int[]和普通的int[]是共享一个，所以要另外搞个类型

            public bool IsParams = false;

            public bool IsOptional = false;

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
            public string ModuleFileName;
            public string AssemblyName;
            public bool IsValueType;

            public TypeInfoCollected BaseType;

            public bool Extends = false;

            public string ExtendsTypeName;

            public MethodInfoCollected[] Methods = EmptyMethodInfos;

            public bool HasExtensionMethods = false;

            public MethodInfoCollected[] ExtensionMethods = EmptyMethodInfos;

            public PropertyInfoCollected[] Properties = EmptyPropertyInfos;

            public bool IsEnum = false;

            public bool IsInterface = false;

            public bool IsDelegate = false;

            public string DelegateParmaters;
            public string DelegateReturnType;

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
            var isParams = parameterDefinition.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.ParamArrayAttribute");
            return new ParameterInfoCollected()
            {
                Name = parameterDefinition.Name,
                ParameterType = CollectInfo(parameterDefinition.ParameterType),
                IsParams = isParams,
                IsOptional = parameterDefinition.IsOptional,
                ParamerterTypeScriptName = Utils.GetTypeScriptName(parameterDefinition.ParameterType.IsRequiredModifier ? parameterDefinition.ParameterType.GetElementType() : parameterDefinition.ParameterType, isParams),
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
                if (Utils.IsDelegate(typeDef) && typeDef.FullName != "System.MulticastDelegate")
                {
                    var invoke = typeDef.Methods.First(m => m.Name == "Invoke");
                    AddRefedType(invoke.ReturnType);
                    foreach(var pi in invoke.Parameters)
                    {
                        AddRefedType(pi.ParameterType);
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
            if (typeReference.IsByReference)
            {
                AddRefedType((typeReference as ByReferenceType).ElementType);
                return;
            }
            if (typeReference.IsArray)
            {
                AddRefedType((typeReference as ArrayType).ElementType);
                return;
            }
            typesRefed.Add(rawType);
        }

        static PropertyInfoCollected CollectInfo(FieldDefinition fieldDefinition)
        {
            if (!fieldDefinition.IsPublic)
            {
                return null;
            }
            AddRefedType(fieldDefinition.FieldType);
            var fi = CollectInfo(fieldDefinition.FieldType);
            if (fi.FullName.Contains('*') || IsDelegateWithPointer(fi))
            {
                return null;
            }
            return new PropertyInfoCollected()
            {
                Name = fieldDefinition.Name,
                IsStatic = fieldDefinition.IsStatic,
                PropertyType = fi,
                DocumentLines = ToLines(DocResolver.GetTsDocument(fieldDefinition)),
            };
        }

        static string trimByDot(string str)
        {
            int idx = str.LastIndexOf('.');
            return (idx == -1) ? str : str.Substring(idx + 1);
        }

        static bool IsDelegateWithPointer(TypeInfoCollected ti)
        {
            return ti.IsDelegate && (ti.DelegateParmaters.Contains('*')  || ti.DelegateReturnType.Contains('*'));
        }

        static PropertyInfoCollected CollectInfo(PropertyDefinition propertyDefinition)
        {
            bool explicitInterfaceImplementation = propertyDefinition.Name.Contains('.');
            bool getterPublic = propertyDefinition.GetMethod != null ? propertyDefinition.GetMethod.IsPublic : false;
            bool setterPublic = propertyDefinition.SetMethod != null ? propertyDefinition.SetMethod.IsPublic : false;
            if (!getterPublic && !setterPublic && !explicitInterfaceImplementation)
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
            var pi = CollectInfo(propertyDefinition.PropertyType);
            if (pi.FullName.Contains('*') || IsDelegateWithPointer(pi))
            {
                return null;
            }
            AddRefedType(propertyDefinition.PropertyType);
            return new PropertyInfoCollected()
            {
                Name = trimByDot(propertyDefinition.Name),
                IsStatic = IsStatic,
                AsMethod = !propertyDefinition.DeclaringType.IsInterface && !explicitInterfaceImplementation,
                Getter = getterPublic,
                Setter = setterPublic,
                PropertyType = pi,
                ContainsGenericParameter = containsGenericParameter,
                DocumentLines = ToLines(DocResolver.GetTsDocument(propertyDefinition))
            };
        }

        static List<MethodDefinition> ExtensionMethods = new List<MethodDefinition>();

        static MethodInfoCollected CollectInfo(MethodDefinition methodDefinition)
        {
            return CollectInfo(methodDefinition, false);
        }

        static MethodInfoCollected CollectInfo(MethodDefinition methodDefinition, bool asExtensionMethod)
        {
            bool explicitInterfaceImplementation = methodDefinition.Name.Contains('.');
            if (!methodDefinition.IsPublic && !explicitInterfaceImplementation)
            {
                return null;
            }

            if (!asExtensionMethod && methodDefinition.IsStatic && methodDefinition.Parameters.Count > 0 && Utils.IsExtension(methodDefinition) && Utils.IsSupportedMethod(methodDefinition))
            {
                ExtensionMethods.Add(methodDefinition);
            }
            if (!asExtensionMethod && ((methodDefinition.ContainsGenericParameter && methodDefinition.IsStatic) || methodDefinition.HasGenericParameters))
            {
                return null;
            }
            var parameters = methodDefinition.Parameters.Skip(asExtensionMethod ? 1 : 0).Select(CollectInfo).ToArray();
            if (parameters.Length > 0)
            {
                parameters[0].IsFirst = true;
                parameters[parameters.Length - 1].IsLast = true;
            }

            if (!methodDefinition.IsConstructor)
            {
                if(Utils.WithPointer(methodDefinition.ReturnType))
                {
                    return null;
                }
                AddRefedType(methodDefinition.ReturnType);
            }

            foreach (var parameterType in methodDefinition.Parameters.Skip(asExtensionMethod ? 1 : 0).Select(p => p.ParameterType))
            {
                if (Utils.WithPointer(parameterType))
                {
                    return null;
                }
                AddRefedType(parameterType);
            }

            return new MethodInfoCollected()
            {
                Name = trimByDot(methodDefinition.IsConstructor ? "constructor" : methodDefinition.Name),
                IsStatic = methodDefinition.IsStatic,
                ReturnType = CollectInfo(methodDefinition.ReturnType),
                Parameters = parameters,
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
                    ModuleFileName = typeReference.Module.FileName,
                    AssemblyName = typeReference.Module.Assembly.Name.Name,
                    IsValueType = typeReference.IsValueType,
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
                        fillBaseInfo(res, typeDef);
                    }
                }
                catch { }
            }

            return res;
        }

        internal static GenerateHookConfigure[] enumGenerateHooks = new GenerateHookConfigure[0];

        static void fillBaseInfo(TypeInfoCollected info, TypeDefinition type)
        {
            if (type.IsNested)
            {
                var nsp = new List<string>();
                var temp = type;
                while (temp.IsNested)
                {
                    nsp.Add(temp.DeclaringType.Name.Replace('`', '$'));
                    temp = temp.DeclaringType;
                }
                nsp.Reverse();
                if (!string.IsNullOrEmpty(temp.Namespace))
                {
                    info.Namespace = temp.Namespace + '.' + string.Join(".", nsp.ToArray());
                }
                else
                {
                    info.Namespace = string.Join(".", nsp.ToArray());
                }
            }

            info.IsEnum = type.IsEnum;
            if (info.IsEnum)
            {
                foreach (var hook in enumGenerateHooks)
                {
                    if (Regex.IsMatch(info.FullName, hook.Pattern))
                    {
                        info.DeclareKeyword = hook.DeclareKeyword;
                        info.EnumKeyValues = Render.StringToString(hook.BodyTemplate, info);
                        info.Proceed = true;
                    }
                }
                if (!info.Proceed)
                {
                    info.EnumKeyValues = "{ " + string.Join(", ", type.Fields.Where(f => f.Name != "value__" && f.IsPublic).Select(f => f.Name + " = " + f.Constant)) + " }";
                    info.DeclareKeyword = "enum";
                }
                info.Proceed = true;
                return;
            }

            info.IsDelegate = Utils.IsDelegate(type);
            if (info.IsDelegate)
            {
                info.DeclareKeyword = "interface";
                if (type.FullName == "System.MulticastDelegate")
                {
                    info.DelegateParmaters = "...args:any[]";
                    info.DelegateReturnType = "any";
                }
                else
                {
                    var invoke = type.Methods.First(m => m.Name == "Invoke");
                    info.DelegateParmaters = string.Join(", ", invoke.Parameters.Select(pi => $"{pi.Name}: {Utils.GetTypeScriptName(pi.ParameterType)}").ToArray());
                    info.DelegateReturnType = Utils.GetTypeScriptName(invoke.ReturnType);
                }
                info.Proceed = true;
                return;
            }

            info.IsInterface = type.IsInterface;
            if (info.IsInterface)
            {
                info.DeclareKeyword = "interface";
                info.ImplementsKeyword = "extends";
            }

            if (type.BaseType != null)
            {
                AddRefedType(type.BaseType);
                info.BaseType = CollectInfo(type.BaseType);
                info.Extends = true;
                info.ExtendsTypeName = (type.BaseType.IsPrimitive || type.BaseType.FullName == "System.Object") ? type.BaseType.FullName : info.BaseType.TypeScriptName;
            }
        }

        static bool isIndexer(PropertyDefinition property)
        {
            if (property.Name != "Item")
            {
                if (property.Name.Contains('.'))
                {
                    if (!property.Name.EndsWith(".Item"))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            if (property.GetMethod != null && property.GetMethod.Parameters.Count == 0)
            {
                return false;
            }
            if (property.SetMethod != null && property.SetMethod.Parameters.Count == 1)
            {
                return false;
            }
            return true;
        }

        static TypeInfoCollected CollectInfo(TypeDefinition typeDefinition)
        {
            TypeInfoCollected res = CollectInfo(typeDefinition as TypeReference);

            if (res.Proceed) return res;

            fillBaseInfo(res, typeDefinition);

            if (res.Proceed) return res;

            res.Proceed = true;

            res.DocumentLines = ToLines(DocResolver.GetTsDocument(typeDefinition));

            HashSet<string> names = new HashSet<string>();
            foreach (var p in typeDefinition.Properties)
            {
                if (isIndexer(p))
                {
                    continue;
                }
                if (p.GetMethod != null)
                {
                    names.Add(p.GetMethod.Name);
                }
                if (p.SetMethod != null)
                {
                    names.Add(p.SetMethod.Name);
                }
            }

            var methods = typeDefinition.Methods
                .Where(m => !(m.IsStatic && m.IsConstructor) && (!m.IsSpecialName || !names.Contains(m.Name)))
                .Where(m => !m.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.ObsoleteAttribute"))
                .Where(m => !isCompilerGenerated(m));

            var publicMethods = methods.Where(m => m.IsPublic);

            List<MethodDefinition> mustAdd = new List<MethodDefinition>();
            if (typeDefinition.IsAbstract)
            {
                addInterfaceMethods(publicMethods
                    .Where(m => !m.IsConstructor && !m.HasGenericParameters)
                    .GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.Cast<MethodDefinition>()), typeDefinition.IsInterface, typeDefinition, mustAdd, false);
            }

            if (!typeDefinition.IsInterface)
            {
                findSameNameButNotOverride(publicMethods
                    .Where(m => !m.IsConstructor && !m.HasGenericParameters)
                    .Concat(mustAdd).GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.Cast<MethodDefinition>()), typeDefinition, typeDefinition, mustAdd);
            }

            res.Methods = methods
                .Concat(mustAdd.Where(m => !m.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.ObsoleteAttribute")))
                // m.HasGenericParameters代表它自身有泛型参数，而如果它用了DeclaringType的泛型参数那么ContainsGenericParameter也为true
                .Select(CollectInfo)
                .Where(mi => mi != null)
                .ToArray();

            res.Properties = typeDefinition.Properties
                .Where(p => !Utils.WithPointer(p.PropertyType))
                .Where(p => !p.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.ObsoleteAttribute"))
                .DistinctBy(p => p.Name)
                .Where(p => !isIndexer(p))
                .Select(CollectInfo)
                .Where(p => p != null)
                .Where(pi => !pi.ContainsGenericParameter || !pi.IsStatic)
                .Concat(typeDefinition.Fields
                    .Where(f => !Utils.WithPointer(f.FieldType) && (!f.ContainsGenericParameter || !f.IsStatic))
                    .Where(f => !f.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.ObsoleteAttribute"))
                    .Where(f => !isCompilerGenerated(f.FieldType))
                    .Select(CollectInfo)
                )
                .Where(p => p != null)
                .DistinctBy(p => new { p.Name, p.IsStatic})
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

        static void findSameNameButNotOverride(Dictionary<string, IEnumerable<MethodDefinition>> methodMap, TypeDefinition addTo, TypeReference type, List<MethodDefinition> result)
        {
            try
            {
                var typeDef = type.Resolve();

                if (typeDef != null)
                {
                    if (addTo != typeDef)
                    {
                        var genericInstanceType = addTo.BaseType as GenericInstanceType;
                        var findGenericArgument = (GenericParameter gp) =>
                        {
                            for (var i = 0; i < typeDef.GenericParameters.Count; ++i)
                            {
                                if (typeDef.GenericParameters[i] == gp)
                                {
                                    return genericInstanceType.GenericArguments[i];
                                }
                            }
                            return null;
                        };

                        foreach (var m in typeDef.Methods)
                        {
                            if (m.HasGenericParameters)
                            {
                                continue;
                            }
                            if (m.ContainsGenericParameter)
                            {
                                if (m.IsStatic && m.IsPublic && !m.IsConstructor && !m.IsSpecialName && type.IsGenericInstance)
                                {
                                    var returnType = m.ReturnType.IsGenericParameter ? findGenericArgument(m.ReturnType as GenericParameter) : m.ReturnType;
                                    if (returnType != null)
                                    {
                                        var addMethod = new MethodDefinition(m.Name, m.Attributes, returnType);
                                        foreach(var pd in m.Parameters)
                                        {
                                            var parameterType = pd.ParameterType.IsGenericParameter ? findGenericArgument(pd.ParameterType as GenericParameter) : pd.ParameterType;
                                            if (parameterType != null)
                                            {
                                                addMethod.Parameters.Add(new ParameterDefinition(pd.Name, pd.Attributes, parameterType));
                                            }
                                            else
                                            {
                                                addMethod = null;
                                                break;
                                            }
                                        }
                                        if (addMethod != null)
                                        {
                                            addTo.Methods.Add(addMethod);
                                        }
                                    }
                                }
                                continue;
                            }
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
                        }

                        foreach (var pd in typeDef.Properties)
                        {
                            bool isStatic = pd.GetMethod != null ? pd.GetMethod.IsStatic : (pd.SetMethod != null ? pd.SetMethod.IsStatic: false);
                            if (isStatic && pd.PropertyType.IsGenericParameter)
                            {
                                
                                var propertyType = findGenericArgument(pd.PropertyType as GenericParameter);
                                if (propertyType != null)
                                {
                                    var addPropery = new PropertyDefinition(pd.Name, pd.Attributes, propertyType);
                                    if (pd.GetMethod != null)
                                    {
                                        addPropery.GetMethod = new MethodDefinition(pd.GetMethod.Name, pd.GetMethod.Attributes, propertyType);
                                        addTo.Methods.Add(addPropery.GetMethod);
                                    }
                                    if (pd.SetMethod != null)
                                    {
                                        addPropery.SetMethod = new MethodDefinition(pd.SetMethod.Name, pd.SetMethod.Attributes, pd.SetMethod.ReturnType);
                                        addPropery.SetMethod.Parameters.Add(new ParameterDefinition(pd.SetMethod.Parameters[0].Name, pd.SetMethod.Parameters[0].Attributes, propertyType));
                                        addTo.Methods.Add(addPropery.SetMethod);
                                    }
                                    addTo.Properties.Add(addPropery);
                                }
                            }
                        }
                    }
                    if (typeDef.BaseType != null)
                    {
                        findSameNameButNotOverride(methodMap, addTo, typeDef.BaseType, result);
                    }
                }
            } catch { }
        }

        static void addInterfaceMethods(Dictionary<string, IEnumerable<MethodDefinition>> methodMap, bool addToInterface, TypeDefinition itf, List<MethodDefinition> result, bool add)
        {
            if (add)
            {
                result.AddRange(itf.Methods.Where(m => {
                    var r = methodMap.ContainsKey(m.Name);
                    return addToInterface ? r : !r;
                    }));
            }

            foreach (var itfInfo in itf.Interfaces)
            {
                try
                {
                    var td = itfInfo.InterfaceType.Resolve();
                    if (td != null)
                    {
                        addInterfaceMethods(methodMap, addToInterface, td, result, true);
                    }
                } catch { }
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
            //try
            //{
            //    if (typeDefinition.BaseType != null)
            //    {
            //        retrieveInterfacesOfClass(typeDefinition.BaseType.Resolve(), infos);
            //    }
            //}
            //catch { }
        }

        internal static GenCodeData Collect(IEnumerable<TypeDefinition> typesToGen)
        {
            var typeInfosToGen = typesToGen.Distinct().Select(CollectInfo).ToArray(); // force referenced types found
            var typesToGenLookup = typesToGen.ToDictionary(t => t.FullName);

            var groupedExtensionMethods = ExtensionMethods.GroupBy(m => {
                try
                {
                    var td = Utils.GetExtendedType(m).Resolve();
                    return typesToGenLookup[td.FullName];
                }
                catch { return null; }
                }).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.Cast<MethodDefinition>());
            foreach(var kv in groupedExtensionMethods)
            {
                //Console.WriteLine($"ExtensionMethod for: {kv.Key}, {kv.Value.Count()}");
                try
                {
                    if (kv.Key != null && !kv.Key.IsEnum)
                    {
                        var info = CollectInfo(kv.Key);
                        info.HasExtensionMethods = true;
                        info.ExtensionMethods = kv.Value.Select(m => CollectInfo(m, true)).ToArray();
                    }
                } catch { }
            }

            return new GenCodeData
            {
                Namespaces = typesRefed.Where(t => !typesToGenLookup.ContainsKey(t.FullName)).DistinctBy((t) => t.FullName).Select(CollectInfo).Concat(typeInfosToGen)
                    .Where(ti => !IsDelegateWithPointer(ti))
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
