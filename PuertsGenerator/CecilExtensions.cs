using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

namespace PuertsGenerator
{
    internal static class CecilExtensions
    {
        /// <summary>
        /// 判断一个类型是否是delegate
        /// </summary>
        /// <param name="typeDefinition">要判断的类型</param>
        /// <returns></returns>
        public static bool IsDelegate(this TypeDefinition typeDefinition)
        {
            if (typeDefinition.BaseType == null)
            {
                return false;
            }
            return typeDefinition.BaseType.FullName == "System.MulticastDelegate";
        }
    }
}
