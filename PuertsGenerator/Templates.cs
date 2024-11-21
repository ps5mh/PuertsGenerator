using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PuertsGenerator
{
    internal static class Templates
    {
        public const string IndexDTs = @"{{<ParameterList}}{{ #Parameters }}${{Name}}: {{ #ParameterType }}{{{TypeScriptName}}}{{/ParameterType}}{{^IsLast}}, {{/IsLast}}{{/Parameters}}{{/ParameterList}}
declare namespace CS {
    //keep type incompatibility / 此属性保持类型不兼容
    const __keep_incompatibility: unique symbol;
    interface $Ref<T> {
        __doNoAccess: T
    }
    namespace System {
        interface Array$1<T> extends System.Array {
            get_Item(index: number):T;
            set_Item(index: number, value: T):void;
        }
    }
    interface $Task<T> {}
{{ #Namespaces }}
namespace {{ Name }} {
{{ #Types }}
    {{ #DocumentLines }}
    {{.}}{{ /DocumentLines }}
    {{ #IsEnum }}
    enum {{{ Name }}} { {{{EnumKeyValues}}} }
    {{ /IsEnum }}
    {{ ^IsEnum }}
    class {{{ Name }}} extends {{ #BaseType }}{{{TypeScriptName}}}{{/BaseType}} {
        protected [__keep_incompatibility]: never;

        {{ #Properties }}
        {{ #DocumentLines }}
        {{.}}{{ /DocumentLines }}
        public {{ #IsStatic }}static {{/IsStatic}}{{ #IsReadOnly }}get {{/IsReadOnly}}{{Name}}: {{ #PropertyType }}{{{TypeScriptName}}}{{/PropertyType}};
        {{/Properties}}

        {{ #Methods }}
        {{ #DocumentLines }}
        {{.}}{{ /DocumentLines }}
        public {{ #IsStatic }}static {{/IsStatic}}{{ #ReturnType }}{{{TypeScriptName}}}{{/ReturnType}} {{Name}}({{>ParameterList}});
        {{/Methods}}

    }
    {{ /IsEnum }}

{{/Types}}
}

{{/Namespaces}}
}
";
    }
}
