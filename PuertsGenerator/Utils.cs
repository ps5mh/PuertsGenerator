using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nustache.Compilation;
using Mono.Cecil;
using System.Runtime.CompilerServices;

#nullable disable

namespace PuertsGenerator
{
    internal class Utils
    {
        public static string GetTypeScriptName(TypeReference type, bool isParams = false)
        {
            TypeSystem typesystem = type.Module.TypeSystem;

            if (type.FullName == typesystem.Int32.FullName)
                return "number";
            if (type.FullName == typesystem.UInt32.FullName)
                return "number";
            else if (type.FullName == typesystem.Int16.FullName)
                return "number";
            else if (type.FullName == typesystem.Byte.FullName)
                return "number";
            else if (type.FullName == typesystem.SByte.FullName)
                return "number";
            else if (type.FullName == typesystem.Char.FullName)
                return "number";
            else if (type.FullName == typesystem.UInt16.FullName)
                return "number";
            else if (type.FullName == typesystem.Boolean.FullName)
                return "boolean";
            else if (type.FullName == typesystem.Int64.FullName)
                return "bigint";
            else if (type.FullName == typesystem.UInt64.FullName)
                return "bigint";
            else if (type.FullName == typesystem.Single.FullName)
                return "number";
            else if (type.FullName == typesystem.Double.FullName)
                return "number";
            else if (type.FullName == typesystem.String.FullName)
                return "string";
            else if (type.FullName == typesystem.Void.FullName)
                return "void";
            else if (type.FullName == "Puerts.ArrayBuffer")
                return "ArrayBuffer";
            else if (type.FullName == typesystem.Object.FullName)
                return "any";
            else if (type.FullName == "System.Delegate" || type.FullName == "Puerts.GenericDelegate")
                return "Function";
            else if (type.FullName == "System.Threading.Tasks.Task")
                return "$Task<any>";
            else if (type.IsByReference)
                return "$Ref<" + GetTypeScriptName((type as ByReferenceType).ElementType) + ">";
            else if (type.IsRequiredModifier)
            {
                return GetTypeScriptName(type.GetElementType());
            }
            else if (type.IsArray)
            {
                var elementType = (type as ArrayType).ElementType;
                return isParams ? (GetTypeScriptName(elementType) + "[]") : ("System.Array$1<" + GetTypeScriptName(elementType) + ">");
            }
            else if (type.IsGenericInstance)
            {
                var genericInstanceType = type as GenericInstanceType;
                if (genericInstanceType.ElementType.Name == "Nullable`1" && genericInstanceType.ElementType.Namespace == "System")
                {
                    return GetTypeScriptName(genericInstanceType.GenericArguments[0]) + " | null";
                }
                var fullName = type.FullName == null ? type.ToString() : type.FullName;
                var parts = fullName.Replace('+', '.').Split('`');
                var argTypenames = genericInstanceType.GenericArguments
                    .Select(x => GetTypeScriptName(x)).ToArray();
                var pos = 0;
                for(; pos < parts[1].Length; pos++)
                {
                    if (parts[1][pos] < '0' || parts[1][pos] > '9') break;
                }
                return parts[0].Replace('/', '.') + '$' + parts[1].Substring(0, pos) + "<" + string.Join(", ", argTypenames) + ">";
            }
            else if (type.FullName == null)
            {
                return type.ToString().Replace('/', '.');
            }
            else
            {
                return type.FullName.Replace('/', '.');
            }
        }

        public static TypeReference GetRawType(TypeReference type)
        {
            if (type.IsByReference )
            {
                return GetRawType((type as ByReferenceType).ElementType);
            }
            if (type.IsArray)
            {
                return GetRawType((type as ArrayType).ElementType);
            }
            if (type.IsGenericInstance)
            {
                var et = (type as GenericInstanceType).ElementType;
                try
                {
                    var etd = et.Resolve();
                    if (etd != null)
                    {
                        return etd;
                    }
                }
                catch { }
                return et;
            }
            return type;
        }

        public static bool IsDelegate(TypeReference type)
        {
            if (type == null)
            {
                return false;
            }
            if (type.FullName == "System.MulticastDelegate")
            {
                return true;
            }
            try
            {
                var td = type.Resolve();
                if (td != null)
                {
                    return IsDelegate(td.BaseType);
                }
            }
            catch { }
            return false;
        }

        public static bool WithPointer(TypeReference type)
        {
            if (type.IsPointer) return true;
            if (type.IsByReference)
            {
                return WithPointer((type as ByReferenceType).ElementType);
            }
            if (type.IsArray)
            {
                return WithPointer((type as ArrayType).ElementType);
            }
            // 加这个太慢了
            /*if (IsDelegate(type))
            {
                if (type.IsGenericInstance)
                {
                    var genericInstanceType = (type as GenericInstanceType);
                    foreach (var gt in genericInstanceType.GenericArguments)
                    {
                        if(WithPointer(gt))
                        {
                            return true;
                        }
                    }
                }
                try
                {
                    var typeDef = type.Resolve();
                    if (typeDef != null)
                    {
                        var invoke = typeDef.Methods.First(m => m.Name == "Invoke");
                        if (WithPointer(invoke.ReturnType) || invoke.Parameters.Any(pi => WithPointer(pi.ParameterType)))
                        {
                            return true;
                        }
                    }
                }
                catch { }
            }*/
            return false;
        }

        public static bool IsExtension(MethodDefinition method)
        {
            return method.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");
        }

        public static bool NotGenericParameterOrIsRef(TypeReference type, List<GenericParameter> refGenericParameters)
        {
            if (type.IsGenericInstance)
            {
                var genericInstanceType = type as GenericInstanceType;
                foreach(var ga in genericInstanceType.GenericArguments)
                {
                    if (!NotGenericParameterOrIsRef(ga, refGenericParameters))
                    {
                        return false;
                    }
                }
            }
            else if (type.IsGenericParameter)
            {
                var genericParameter = type as GenericParameter;
                if (genericParameter.Constraints.Count == 0)
                {
                    return false;
                }

                foreach(var constraint in genericParameter.Constraints)
                {
                    if (constraint.ConstraintType.IsValueType || constraint.ConstraintType.FullName == "System.ValueType" || constraint.ConstraintType.IsGenericInstance)
                    {
                        return false;
                    }
                }

                refGenericParameters.Add(genericParameter);
            }
            return true;
        }

        public static bool IsSupportedMethod(MethodDefinition method)
        {
            if (!method.ContainsGenericParameter)
            {
                return true;
            }
            if (method.Parameters.Count > 0)
            {
                if (method.Parameters[0].ParameterType.IsByReference)
                {
                    return false;
                }
            }
            List<GenericParameter> argRefedGenericParameters = new List<GenericParameter>();
            if (method.Parameters.Any(pi => !NotGenericParameterOrIsRef(pi.ParameterType, argRefedGenericParameters)))
            {
                return false;
            }

            // if return is GenericParameter, must infer by argument
            List<GenericParameter> retRefedGenericParameters = new List<GenericParameter>();
            if ( !NotGenericParameterOrIsRef(method.ReturnType, retRefedGenericParameters))
            {
                return false;
            }
            foreach (var rrgp in retRefedGenericParameters)
            {
                if (!argRefedGenericParameters.Contains(rrgp))
                {
                    return false;
                }
            }

            return true;
        }

        public static TypeReference GetExtendedType(MethodDefinition method)
        {
            var type = method.Parameters[0].ParameterType;
            if (type.IsGenericParameter)
            {
                var genericParameter = type as GenericParameter;
                foreach (var constraint in genericParameter.Constraints)
                {
                    if (!constraint.ConstraintType.IsValueType)
                    {
                        return constraint.ConstraintType;
                    }
                }
                return null;
            }
            return type;
        }
    }
}
