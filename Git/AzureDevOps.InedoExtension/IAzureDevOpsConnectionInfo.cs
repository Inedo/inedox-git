using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.AzureDevOps.Credentials;

namespace Inedo.Extensions.AzureDevOps
{
    internal interface IAzureDevOpsConnectionInfo
    {
        string InstanceUrl { get; }
        SecureString Token { get; }
    }

    internal interface IAzureDevOpsConfiguration : IAzureDevOpsConnectionInfo
    {
        string CredentialName { get; }
        string ResourceName { get; }
    }

    internal static class AzureDevOpsOperationExtensions
    {
        public static (AzureDevOpsSecureCredentials, AzureDevOpsSecureResource) GetCredentialsAndResource(this IAzureDevOpsConfiguration operation, ICredentialResolutionContext context)
        {
            // ProjectName could be set directly (via OtterScript) or indirectly (via legacy ResourceCredential)
            if (string.IsNullOrEmpty(operation.InstanceUrl))
            {
                // for backwards-compatibility, treat the LegacyResourceCredentialName as a ResourceName
                var resourcename = AH.CoalesceString(operation.CredentialName, operation.ResourceName);
                var resource = SecureResource.TryCreate(resourcename, context) as AzureDevOpsSecureResource;
                return ((AzureDevOpsSecureCredentials)resource.GetCredentials(context), resource);
            }
            else
            {
                return (
                    new AzureDevOpsSecureCredentials
                    {
                        Token = operation.Token
                    },
                    new AzureDevOpsSecureResource
                    {
                        InstanceUrl = operation.InstanceUrl
                    });
            }
        }
    }
}
