using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;

namespace Inedo.Extensions.GitHub.Operations
{
    [Obsolete("This should be renamed Git::Set-CommitStatus", true)]
    [DisplayName("[Obsolete] Set GitHub Build Status")]
    [Description("Sets a status message on a GitHub commit. This should be renamed Git::Set-CommitStatus")]
    [Example(@"try
{
    GitHub::Set-Status (
        Status = pending,
        ...
    );

    ...
}
catch
{
    # make sure the status is set even if the build fails.
    error;
}

GitHub::Set-Status (
    Status = auto,
    ...
);")]
    [ScriptAlias("Set-Status")]
    [ScriptAlias("GitHub-Set-Status", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    [Tag("source-control")]
    public sealed class GitHubSetStatusOperation : GitHubOperationBase
    {
        public enum StatusType
        {
            pending,
            auto,
            success,
            failure,
            error
        }

        [DisplayName("Additional context")]
        [Description("Appears in the commit status dialog on GitHub after \"ci/buildmaster\". Used to differentiate between multiple BuildMaster statuses on the same commit. In most cases, it is safe to leave this blank.")]
        [ScriptAlias("AdditionalContext")]
        public string AdditionalContext { get; set; }

        [Required]
        [DisplayName("Git commit hash")]
        [ScriptAlias("CommitHash")]
        public string CommitHash { get; set; }

        [Required]
        [DisplayName("Status")]
        [ScriptAlias("Status")]
        public StatusType Status { get; set; }

        [Category("Descriptions")]
        [DisplayName("Description")]
        [ScriptAlias("Description")]
        [Description("Used for all statuses except 'auto'")]
        [DefaultValue("#$ExecutionId in progress...")]
        public string Description { get; set; }

        [Category("Descriptions")]
        [DisplayName("Complete (success)")]
        [ScriptAlias("NormalDescription")]
        [DefaultValue("#$ExecutionId completed.")]
        public string NormalDescription { get; set; }

        [Category("Descriptions")]
        [DisplayName("Complete (warning)")]
        [ScriptAlias("WarningDescription")]
        [DefaultValue("#$ExecutionId completed with warnings.")]
        public string WarningDescription { get; set; }

        [Category("Descriptions")]
        [DisplayName("Complete (error)")]
        [ScriptAlias("ErrorDescription")]
        [DefaultValue("#$ExecutionId failed!")]
        public string ErrorDescription { get; set; }

        public override async Task ExecuteAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var client = new GitHubClient(credentials, resource, this);

            this.LogWarning("This operation (GitHub::Set-Status) is obsolete, and should be replaced with Git::Set-CommitStatus.");

            string url = null;
            if (string.IsNullOrEmpty(SDK.BaseUrl))
                this.LogWarning("BaseUrl is not set, which means a target_url cannot be provided as a status. Go to Admin > Advanced/All Settings to set a BaseUrl.");
            else
                url = $"{SDK.BaseUrl.TrimEnd('/')}/executions/execution-in-progress?executionId={context.ExecutionId}";

            var statusContext = ("ci/" + SDK.ProductName.ToLower() + "/" + this.AdditionalContext).TrimEnd('/');

            if (this.Status == StatusType.auto)
            {
                switch (context.ExecutionStatus)
                {
                    case ExecutionStatus.Normal:
                        this.Status = StatusType.success;
                        this.Description = this.NormalDescription;
                        break;
                    case ExecutionStatus.Warning:
                        this.Status = StatusType.success;
                        this.Description = this.WarningDescription;
                        break;
                    case ExecutionStatus.Error:
                        this.Status = StatusType.failure;
                        this.Description = this.ErrorDescription;
                        break;
                    case ExecutionStatus.Fault:
                    default:
                        this.Status = StatusType.error;
                        this.Description = this.ErrorDescription;
                        break;
                }
            }

            this.LogInformation($"Assigning '{this.Status}' status to the commit on GitHub...");
            await client.CreateStatusAsync(AH.CoalesceString(resource.OrganizationName, credentials.UserName), resource.RepositoryName, this.CommitHash, this.Status.ToString(), url, this.Description, statusContext, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Set build status on GitHub commit ", new Hilite(config[nameof(CommitHash)]), " to ", new Hilite(config[nameof(Status)])),
                new RichDescription("in ", new Hilite(config.DescribeSource()))
            );
        }
    }
}
