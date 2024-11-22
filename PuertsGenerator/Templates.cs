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
{{ ^IsGlobal }} namespace {{ Name }} {{ /IsGlobal }}{
{{ #Types }}
    {{ #DocumentLines }}
    {{.}}{{ /DocumentLines }}
    {{ #IsEnum }}
    enum {{{ Name }}} { {{{EnumKeyValues}}} }
    {{ /IsEnum }}
    {{ ^IsEnum }}
    {{DeclareKeyword}} {{{ Name }}}{{#HasGenericParameters}}<{{#GenericParameters}}{{Name}}{{^IsLast}}, {{/IsLast}}{{/GenericParameters}}>{{/HasGenericParameters}}{{ #BaseType }} extends {{{Namespace}}}.{{{Name}}}{{/BaseType}}{{#WithImplements}} implements {{/WithImplements}}{{{Implements}}} {
        {{#IsInterface}}protected [__keep_incompatibility]: never;{{/IsInterface}}

        {{ #Properties }}
        {{ #DocumentLines }}
        {{.}}{{ /DocumentLines }}
        public {{ #IsStatic }}static {{/IsStatic}}{{ #IsReadOnly }}get {{/IsReadOnly}}{{Name}}{{ #IsReadOnly }}(){{/IsReadOnly}}: {{ #PropertyType }}{{{TypeScriptName}}}{{/PropertyType}};
        {{/Properties}}

        {{ #Methods }}
        {{ #DocumentLines }}
        {{.}}{{ /DocumentLines }}
        public {{ #IsStatic }}static {{/IsStatic}}{{Name}}({{>ParameterList}}){{ ^IsConstructor }}: {{ #ReturnType }}{{{TypeScriptName}}}{{/ReturnType}}{{ /IsConstructor }};
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
