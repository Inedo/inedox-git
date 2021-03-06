﻿using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.AzureDevOps.VariableTemplates
{
    [DisplayName("Azure DevOps CommitId")]
    [Description("CommitId within a GitHub repository.")]
    public sealed class CommitIdVariable : VariableTemplateType
    {
        [Persistent]
        [DisplayName("From Azure DevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsSecureResource>))]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Azure DevOps resource")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsSecureCredentials;
            if (resource == null)
            {
                var rc = SecureCredentials.TryCreate(this.ResourceName, new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsCredentials;
                resource = (AzureDevOpsSecureResource)rc?.ToSecureResource();
                credential = (AzureDevOpsSecureCredentials)rc?.ToSecureCredentials();
            }
            if (resource == null || !Uri.TryCreate(resource.InstanceUrl.TrimEnd('/'), UriKind.Absolute, out var parsedUri))
                return new LiteralHtml(value.AsString());

            return new A($"{resource.InstanceUrl.TrimEnd('/')}/{resource.ProjectName}/_git/{AH.CoalesceString(this.RepositoryName, resource.RepositoryName)}/commit/{value.AsString()}", value.AsString())
            {
                Class = "ci-icon azuredevops",
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            var repoName = AH.CoalesceString(this.ResourceName, this.RepositoryName);
            return new RichDescription("GitHub (", new Hilite(repoName), ") commit.");
        }
    }
}
