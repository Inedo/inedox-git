using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Build Number")]
    [Description("Build numbers from a specified build definition in Azure DevOps.")]
    public sealed class AzureDevOpsBuildNumberVariableSource : DynamicListVariableType
    {
        [Persistent]
        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsSecureResource>))]
        [Required]
        public string ResourceName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [PlaceholderText("Use project AzureDevOps resource")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Persistent]
        [DisplayName("Build definition")]
        [SuggestableValue(typeof(BuildDefinitionNameSuggestionProvider))]
        [Required]
        public string BuildDefinitionName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsSecureCredentials;
            if (resource == null)
                return Enumerable.Empty<string>();

            var projectName = AH.CoalesceString(this.ProjectName, resource.ProjectName);
            var api = new RestApi(credential?.Token, resource.InstanceUrl, null);

            var definition = await api.GetBuildDefinitionAsync(projectName, this.BuildDefinitionName).ConfigureAwait(false);
            if (definition == null)
                return Enumerable.Empty<string>();

            var builds = await api.GetBuildsAsync(projectName, definition.id).ConfigureAwait(false);
            return builds.Select(b => b.buildNumber);
        }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not AzureDevOpsSecureResource resource || !Uri.TryCreate(resource.InstanceUrl.TrimEnd('/'), UriKind.Absolute, out _))
                return new LiteralHtml(value.AsString());

            // Ideally we would use the GitHubClient to retreive the proper URL, but that's resource intensive and we can guess the convention
            return new A($"{resource.InstanceUrl.TrimEnd('/')}/{AH.CoalesceString(this.ProjectName, resource.ProjectName)}/{this.BuildDefinitionName}/_build/results?buildId={value.AsString()}", value.AsString())
            {
                Class = "ci-icon azuredevops",
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            var description = new RichDescription("Azure DevOps (", new Hilite(this.ResourceName), ") ",  " builds for ", new Hilite(this.BuildDefinitionName));
            if (!string.IsNullOrEmpty(this.ProjectName))
                description.AppendContent(" in ", new Hilite(this.ProjectName));
            description.AppendContent(".");
            return description;
        }
    }
}
