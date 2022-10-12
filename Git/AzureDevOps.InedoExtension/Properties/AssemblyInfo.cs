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
[assembly: AssemblyCopyright("Copyright © Inedo 2022")]
[assembly: AssemblyVersion("2.2.0")]
[assembly: AssemblyFileVersion("2.2.0")]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]
