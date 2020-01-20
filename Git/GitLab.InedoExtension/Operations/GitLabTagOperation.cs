using System;
using System.ComponentModel;
using System.Security;
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
    public sealed class GitLabTagOperation : TagOperation, IGitLabConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitLab resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitLabSecureResource>))]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from GitLab resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from GitLab resource's credentials")]
        public SecureString Password { get; set; }


        [Category("Connection/Identity")]
        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        [PlaceholderText("Use group from GitLab resource")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [PlaceholderText("Use project from GitLab resource")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText("Use URL from GitLab resource")]
        public string ApiUrl { get; set; }

        private GitLabSecureCredentials credential;
        private GitLabSecureResource resource;

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            (this.credential, this.resource) = this.GetCredentialsAndResource((ICredentialResolutionContext)context);
            return base.ExecuteAsync(context);
        }
        protected override Extensions.Credentials.UsernamePasswordCredentials GetCredentials() => this.credential?.ToUsernamePassword();

        protected override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken) => this.resource.GetRepositoryUrl(context, cancellationToken);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag GitLab Source"),
               new RichDescription("in ", new Hilite(config.DescribeSource()), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
