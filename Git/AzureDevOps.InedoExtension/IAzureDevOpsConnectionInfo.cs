namespace Inedo.Extensions.AzureDevOps
{
    internal interface IAzureDevOpsConnectionInfo
    {
        string UserName { get; }
        string Password { get; }
        string InstanceUrl { get; }
    }
}
