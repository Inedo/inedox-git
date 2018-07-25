﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Extensions.Operations;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Operations
{
    [DisplayName("Tag GitLab Source")]
    [Description("Tags the source code in a GitLab project.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [ScriptAlias("GitLab-Tag", Obsolete = true)]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    [Example(@"
# tags the current source tree with the current release name and package number
GitLab::Tag(
    Credentials: Hdars-GitLab,
    Group: Hdars,
    Tag: $ReleaseName.$PackageNumber
);
")]
    public sealed class GitLabTagOperation : TagOperation<GitLabCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Category("GitLab")]
        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        [MappedCredential(nameof(GitLabCredentials.GroupName))]
        [PlaceholderText("Use group from credentials")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Category("GitLab")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [MappedCredential(nameof(GitLabCredentials.ProjectName))]
        [PlaceholderText("Use project from credentials")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitLabClient.GitLabComUrl)]
        [Description("Leave this value blank to connect to gitlab.com. For local installations of GitLab, an API URL must be specified.")]
        [MappedCredential(nameof(GitLabCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        protected override async Task<string> GetRepositoryUrlAsync(CancellationToken cancellationToken)
        {
            var gitlab = new GitLabClient(this.ApiUrl, this.UserName, this.Password, this.GroupName);

            var project = await gitlab.GetProjectAsync(this.ProjectName, cancellationToken).ConfigureAwait(false);

            if (project == null)
                throw new InvalidOperationException($"Project '{this.ProjectName}' not found on GitLab.");

            return (string)project["http_url_to_repo"];
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.ProjectName)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Tag GitLab Source"),
               new RichDescription("in ", new Hilite(source), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
