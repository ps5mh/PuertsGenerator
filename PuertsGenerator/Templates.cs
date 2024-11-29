using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PuertsGenerator
{
    internal static class Templates
    {
        public const string IndexDTs = @"{{<ParameterList}}{{ #Parameters }}{{ #IsParams }}...{{ /IsParams }}${{Name}}{{ #IsOptional }}?{{ /IsOptional }}: {{{ParamerterTypeScriptName}}}{{^IsLast}}, {{/IsLast}}{{/Parameters}}{{/ParameterList}}
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
{{ ^IsGlobal }}
namespace {{ Name }} {
{{ /IsGlobal }}
{{ #Types }}
    {{ #DocumentLines }}
    {{.}}{{ /DocumentLines }}
    // {{{ModuleFileName}}}
    {{ #IsEnum }}
    {{DeclareKeyword}} {{{ Name }}} {{{EnumKeyValues}}}
    {{ /IsEnum }}
    {{ ^IsEnum }}
    {{ ^IsDelegate }}
    {{DeclareKeyword}} {{{ Name }}}{{#HasGenericParameters}}<{{#GenericParameters}}{{Name}}{{^IsLast}}, {{/IsLast}}{{/GenericParameters}}>{{/HasGenericParameters}}{{ #Extends }} extends {{{ExtendsTypeName}}}{{/Extends}}{{#WithImplements}} {{ImplementsKeyword}} {{/WithImplements}}{{{Implements}}} {
        {{^IsInterface}}protected [__keep_incompatibility]: never;{{/IsInterface}}

        {{ #Properties }}
        {{ #DocumentLines }}
        {{.}}{{ /DocumentLines }}
        {{ #AsMethod }}
        {{ #Getter }}
        {{^IsInterface}}public {{/IsInterface}}{{ #IsStatic }}static {{/IsStatic}}get {{Name}}(): {{ #PropertyType }}{{{TypeScriptName}}}{{/PropertyType}};
        {{ /Getter }}
        {{ #Setter }}
        {{^IsInterface}}public {{/IsInterface}}{{ #IsStatic }}static {{/IsStatic}}set {{Name}}(value: {{ #PropertyType }}{{{TypeScriptName}}}{{/PropertyType}});
        {{ /Setter }}
        {{ /AsMethod }}
        {{ ^AsMethod }}
        {{^IsInterface}}public {{/IsInterface}}{{ #IsStatic }}static {{/IsStatic}}{{Name}}: {{ #PropertyType }}{{{TypeScriptName}}}{{/PropertyType}};
        {{ /AsMethod }}
        {{/Properties}}

        {{ #Methods }}
        {{ #DocumentLines }}
        {{.}}{{ /DocumentLines }}
        {{^IsInterface}}public {{/IsInterface}}{{ #IsStatic }}static {{/IsStatic}}{{Name}}({{>ParameterList}}){{ ^IsConstructor }}: {{ #ReturnType }}{{{TypeScriptName}}}{{/ReturnType}}{{ /IsConstructor }};
        {{/Methods}}

    }
    {{ /IsDelegate }}
    {{ #IsDelegate }}
    {{DeclareKeyword}} {{{ Name }}}{{#HasGenericParameters}}<{{#GenericParameters}}{{Name}}{{^IsLast}}, {{/IsLast}}{{/GenericParameters}}>{{/HasGenericParameters}} {
        ({{{ DelegateParmaters }}}) : {{{ DelegateReturnType }}}; 
        Invoke?: ({{{ DelegateParmaters }}}) =>  {{{ DelegateReturnType }}};
    }
    {{ ^HasGenericParameters }}
    var  {{{ Name }}}: { new (func: () => void):  {{{ Name }}}; }
    {{/HasGenericParameters}}
    {{ /IsDelegate }}
    {{ /IsEnum }}
    {{ #HasExtensionMethods }}
    interface {{{ Name }}} {
        {{ #ExtensionMethods }}
        {{ #DocumentLines }}
        {{.}}{{ /DocumentLines }}
        {{Name}}({{ #Parameters }}{{ #IsParams }}...{{ /IsParams }}${{Name}}{{ #IsOptional }}?{{ /IsOptional }}: {{{ParamerterTypeScriptName}}}{{^IsLast}}, {{/IsLast}}{{/Parameters}}): {{ #ReturnType }}{{{TypeScriptName}}}{{/ReturnType}};
        {{/ExtensionMethods}}
    }
    {{ /HasExtensionMethods}}

{{/Types}}
{{ ^IsGlobal }}
}
{{ /IsGlobal }}

{{/Namespaces}}
}
";
    }
}
