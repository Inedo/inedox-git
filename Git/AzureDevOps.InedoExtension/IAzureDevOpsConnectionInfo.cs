using System.Security;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.AzureDevOps.Credentials;

namespace Inedo.Extensions.AzureDevOps
{
    internal interface IAzureDevOpsConnectionInfo
    {
        string InstanceUrl { get; }
        string UserName { get; set; }
        SecureString Token { get; }
        string ProjectName { get; }
        string RepositoryName { get; }
    }
    internal interface IAzureDevOpsConfiguration : IAzureDevOpsConnectionInfo
    {
        string ResourceName { get; }
    }
    internal sealed class AzureDevOpsConfiguration : IAzureDevOpsConfiguration
    {
        public string UserName { get; set; }
        public string ResourceName { get; set; }
        public string InstanceUrl { get; set; }
        public SecureString Token { get; set; }
        public string ProjectName { get; set; }
        public string RepositoryName { get; set; }
        public AzureDevOpsConfiguration()
        {
        }
        public AzureDevOpsConfiguration(AzureDevOpsSecureCredentials credentials, AzureDevOpsSecureCredentials resource)
        {
            this.UserName = credentials?.UserName;
            this.Token = credentials?.Token;

        }
    }

    internal static class AzureDevOpsOperationExtensions
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IAzureDevOpsConfiguration.RepositoryName)],
                config[nameof(IAzureDevOpsConfiguration.ProjectName)],
                config[nameof(IAzureDevOpsConfiguration.ResourceName)],
                "(unknown)");
        }
        public static (AzureDevOpsSecureCredentials, AzureDevOpsSecureResource) GetCredentialsAndResource(this IComponentConfiguration config)
        {
            return GetCredentialsAndResource(
                new AzureDevOpsConfiguration
                {
                    UserName = AH.NullIf(config[nameof(IAzureDevOpsConfiguration.UserName)], string.Empty),
                    InstanceUrl = AH.NullIf(config[nameof(IAzureDevOpsConfiguration.InstanceUrl)], string.Empty),
                    ProjectName = AH.NullIf(config[nameof(IAzureDevOpsConfiguration.ProjectName)], string.Empty),
                    Token = string.IsNullOrEmpty(config[nameof(IAzureDevOpsConfiguration.Token)]) ? null : AH.CreateSecureString(config[nameof(IAzureDevOpsConfiguration.Token)]),
                    RepositoryName = AH.NullIf(config[nameof(IAzureDevOpsConfiguration.RepositoryName)], string.Empty),
                    ResourceName = AH.NullIf(config[nameof(IAzureDevOpsConfiguration.ResourceName)], string.Empty)
                },
                new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null));
        }
        public static (AzureDevOpsSecureCredentials, AzureDevOpsSecureResource) GetCredentialsAndResource(this IAzureDevOpsConfiguration operation, IOperationExecutionContext context)
            => GetCredentialsAndResource(operation, (ICredentialResolutionContext)context);
        public static (AzureDevOpsSecureCredentials, AzureDevOpsSecureResource) GetCredentialsAndResource(this IAzureDevOpsConfiguration operation, ICredentialResolutionContext context)
        {
            AzureDevOpsSecureCredentials credentials = null;
            AzureDevOpsSecureResource resource = null;
            if (!string.IsNullOrEmpty(operation.ResourceName))
            {
                resource = (AzureDevOpsSecureResource)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    var rc = SecureCredentials.TryCreate(operation.ResourceName, context) as AzureDevOpsCredentials;
                    resource = (AzureDevOpsSecureResource)rc?.ToSecureResource();
                    credentials = (AzureDevOpsSecureCredentials)rc?.ToSecureCredentials();
                }
                else
                {
                    credentials = (AzureDevOpsSecureCredentials)resource.GetCredentials(context);
                }
            }

            return (
                string.IsNullOrEmpty(AH.CoalesceString(operation.UserName, credentials?.UserName)) ? null : 
                    new AzureDevOpsSecureCredentials
                    {
                        UserName = AH.CoalesceString(operation.UserName, credentials?.UserName),
                        Token = operation.Token ?? credentials?.Token
                    },
                new AzureDevOpsSecureResource
                {
                    InstanceUrl = AH.CoalesceString(operation.InstanceUrl, resource?.InstanceUrl),
                    ProjectName = AH.CoalesceString(operation.ProjectName, resource?.ProjectName),
                    RepositoryName = AH.CoalesceString(operation.RepositoryName, resource?.RepositoryName)
                }
            );

        }
    }
}
