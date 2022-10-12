using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
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
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsRepository>))]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Azure DevOps resource")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not AzureDevOpsRepository resource || !Uri.TryCreate(resource.LegacyInstanceUrl.TrimEnd('/'), UriKind.Absolute, out _))
                return new LiteralHtml(value.AsString());
            
            return new A($"{resource.LegacyInstanceUrl.TrimEnd('/')}/{resource.ProjectName}/_git/{AH.CoalesceString(this.RepositoryName, resource.RepositoryName)}/commit/{value.AsString()}", value.AsString())
            {
                Classes = { "ci-icon", "azuredevops" },
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
