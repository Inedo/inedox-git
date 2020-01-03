using System.Security;

namespace Inedo.Extensions.AzureDevOps
{
    internal interface IAzureDevOpsConnectionInfo
    {
        string InstanceUrl { get; }
        SecureString Token { get; }
    }
}
