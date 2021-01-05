using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Inedo.Extensibility;

[assembly: AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]
[assembly: ScriptNamespace("AzureDevOps", PreferUnqualified = false)]

[assembly: AssemblyTitle("AzureDevOps")]
[assembly: AssemblyDescription("Source control and work item tracking integration for Azure DevOps.")]
[assembly: AssemblyCompany("Inedo, LLC")]
[assembly: AssemblyProduct("any")]
[assembly: AssemblyCopyright("Copyright © Inedo 2019")]
[assembly: AssemblyVersion("0.0.0")]
[assembly: AssemblyFileVersion("0.0.0")]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]
