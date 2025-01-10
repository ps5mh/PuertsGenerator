using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable disable

namespace PuertsGenerator
{
    internal class GenerateHookConfigure
    {
        public string Pattern { get; set; }

        public string DeclareKeyword { get; set; }

        public string BodyTemplate { get; set; }
    }

    internal class AssemblyConfigure
    {
        public HashSet<string> Whitelist { get; set; }

        public HashSet<string> Blacklist { get; set; }
    }

    internal class GenerateConfigure
    {
        public Dictionary<string, AssemblyConfigure> Assemblys { get; set; }

        public GenerateHookConfigure[] EnumGenerateHooks { get; set; }
        public bool CollectAllReferences {  get; set; }
        public string PuertsGenConfigAssemblyPath { get; set; }
        public string[] AssemblyPaths { get; set; }
    }
}
