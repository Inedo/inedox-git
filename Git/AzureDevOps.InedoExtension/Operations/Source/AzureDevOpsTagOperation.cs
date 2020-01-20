﻿using System;
using System.ComponentModel;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Extensions.Operations;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.Operations
{
    [DisplayName("Tag Azure DevOps Source")]
    [Description("Tags the source code in an Azure DevOps project.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [Example(@"
# tags the current source tree with the current release name and build number
AzureDevOps::Tag
(
    Credentials: Hdars-Azure,
    Project: HdarsOrg,
    Repository: HdarsApp,
    Tag: $ReleaseName.$BuildNumber
);
")]
    public sealed class GitLabTagOperation : TagOperation, IAzureDevOpsConfiguration
    {
        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsSecureResource>))]
        [Required]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [PlaceholderText("Use team project from AzureDevOps resource")]
        public string ProjectName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use the project name")]
        public string RepositoryName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Url")]
        [DisplayName("Project collection URL")]
        [PlaceholderText("Use team project from AzureDevOps resource")]
        public string InstanceUrl { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from AzureDevOps resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Token")]
        [DisplayName("Personal access token")]
        [PlaceholderText("Use team project from AzureDevOps resource's credential")]
        public SecureString Token { get; set; }

        private AzureDevOpsSecureCredentials credential;
        private AzureDevOpsSecureResource resource;

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            (this.credential, this.resource) = this.GetCredentialsAndResource(context);
            return base.ExecuteAsync(context);
        }
        protected override Extensions.Credentials.UsernamePasswordCredentials GetCredentials() => this.credential?.ToUsernamePassword();

        protected override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken) 
            => this.resource.GetRepositoryUrlAsync(context, cancellationToken);

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag Azure DevOps Source"),
               new RichDescription("in ", new Hilite(config.DescribeSource()), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
