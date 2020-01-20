using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Credentials
{
    [DisplayName("GitLab Project")]
    [Description("Connect to a GitLab project for source code, issue tracking, etc. integration")]
    public sealed class GitLabSecureResource : GitSecureResourceBase<GitLabSecureCredentials>
    {
        [Persistent]
        [DisplayName("API URL")]
        [PlaceholderText(GitLabClient.GitLabComUrl)]
        [Description("Leave this value blank to connect to gitlab.com. For local installations of GitLab, an API URL must be specified.")]
        public string ApiUrl { get; set; }

        [Persistent]
        [DisplayName("Group name")]
        [PlaceholderText("e.g. apache")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [PlaceholderText("e.g. log4net")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [Required]
        public string ProjectName { get; set; }

        public override RichDescription GetDescription()
        {
            var host = "GitLab.com";
            if (!string.IsNullOrWhiteSpace(this.ApiUrl))
            {
                if (Uri.TryCreate(this.ApiUrl, UriKind.Absolute, out var uri))
                    host = uri.Host;
                else
                    host = "(unknown)";
            }

            var group = string.IsNullOrEmpty(this.GroupName) ? "" : $"{this.GroupName}\\";
            return new RichDescription($"{group}{this.ProjectName} @ {host}");
        }

        public override async Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken)
        {
            var gitlab = new GitLabClient((GitLabSecureCredentials)this.GetCredentials(context), this);
            var project = await gitlab.GetProjectAsync(this.ProjectName, cancellationToken).ConfigureAwait(false);
            if (project == null)
                throw new InvalidOperationException($"Project '{this.ProjectName}' not found on GitLab.");

            return (string)project["http_url_to_repo"];
        }
    }
}
