using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Get Source from GitLab")]
    [Description("Gets the source code from a GitLab project.")]
    [Tag("source-control")]
    [ScriptAlias("GitLab-GetSource")]
    [ScriptNamespace("GitLab", PreferUnqualified = true)]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
GitLab-GetSource(
    Credentials: Hdars-GitLab,
    Group: Hdars,
    DiskPath: ~\Sources
);
")]
    public sealed class GitLabGetSourceOperation : GetSourceOperation<GitLabCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Category("GitLab")]
        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        [MappedCredential(nameof(GitLabCredentials.GroupName))]
        [PlaceholderText("Use group from credentials")]
        [SuggestibleValue(typeof(GroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Category("GitLab")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [MappedCredential(nameof(GitLabCredentials.ProjectName))]
        [PlaceholderText("Use project from credentials")]
        [SuggestibleValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitLabClient.GitLabComUrl)]
        [Description("Leave this value blank to connect to gitlab.com. For local installations of GitLab, an API URL must be specified.")]
        [MappedCredential(nameof(GitLabCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        protected override async Task<string> GetRepositoryUrlAsync()
        {
            var gitlab = new GitLabClient(this.ApiUrl, this.UserName, this.Password, this.GroupName);

            var project = await gitlab.GetProjectAsync(this.ProjectName).ConfigureAwait(false);

            if (project == null)
                throw new InvalidOperationException($"Project '{this.ProjectName}' not found on GitLab.");

            return (string)project["http_url_to_repo"];
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.ProjectName)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Get GitLab Source"),
               new RichDescription("from ", new Hilite(source), " to ", new Hilite(config[nameof(this.DiskPath)]))
            );
        }
    }
}
