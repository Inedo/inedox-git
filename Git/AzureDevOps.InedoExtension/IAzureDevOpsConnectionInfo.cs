namespace Inedo.Extensions.AzureDevOps
{
    internal interface IAzureDevOpsConnectionInfo
    {
        string UserName { get; }
        string Token { get; }
        string Domain { get; }
        string ProjectUrl { get; }
    }
}
