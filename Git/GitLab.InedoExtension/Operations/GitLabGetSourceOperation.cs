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
    [DisplayName("Get Source from GitLab Repository")]
    [Description("Gets the source code from a GitLab project.")]
    [Tag("source-control")]
    [ScriptAlias("Get-Source")]
    [ScriptAlias("GitLab-GetSource", Obsolete = true)]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    [DefaultProperty(nameof(ResourceName))]
    [Example(@"
# pulls source from a GitLab resource and archives/exports the contents to the $WorkingDirectory
GitLab::Get-Source MyGitLabResource;

# pulls source from a GitLab resource (with an overridden project name) and archives/exports the contents to a target directory
GitLab::Get-Source MyGitLabResource
(
    Project: app-$ApplicationName,
    DiskPath: ~\Sources
);
")]
    public sealed class GitLabGetSourceOperation : GetSourceOperation, IGitLabConfiguration
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

        protected override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken) 
            => this.resource.GetRepositoryUrlAsync(context, cancellationToken);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Get GitLab Source"),
               new RichDescription("from ", new Hilite(config.DescribeSource()), " to ", new Hilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
