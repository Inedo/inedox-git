using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Configurations;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Operations;
using IOperationCollectionContext = Inedo.Otter.Extensibility.Operations.IOperationExecutionContext;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Ensure GitLab Release")]
    [Description("Sets the release notes for a tag in a GitLab repository.")]
    [Tag("source-control")]
    [ScriptAlias("Ensure-GitLab-Release")]
    [ScriptNamespace("GitLab", PreferUnqualified = true)]
    public sealed class GitLabEnsureReleaseOperation : EnsureOperation<GitLabReleaseConfiguration>
    {
#if !BuildMaster
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var gitlab = new GitLabClient(this.Template.ApiUrl, this.Template.UserName, this.Template.Password, this.Template.GroupName);

            var tag = await gitlab.GetTagAsync(this.Template.ProjectName, this.Template.Tag, context.CancellationToken).ConfigureAwait(false);

            if (tag == null || !tag.ContainsKey("release") || tag["release"] == null)
            {
                return new GitLabReleaseConfiguration { Exists = false };
            }

            var release = (Dictionary<string, object>)tag["release"];
            return new GitLabReleaseConfiguration
            {
                Tag = (string)release["tag_name"],
                Description = (string)release["description"]
            };
        }
#endif

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var gitlab = new GitLabClient(this.Template.ApiUrl, this.Template.UserName, this.Template.Password, this.Template.GroupName);

            await gitlab.EnsureReleaseAsync(this.Template.ProjectName, this.Template.Tag, this.Template.Description ?? string.Empty, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.Template.ProjectName)], config[nameof(this.Template.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Ensure GitLab Release"),
               new RichDescription("in ", new Hilite(source), " for tag ", new Hilite(config[nameof(this.Template.Tag)]))
            );
        }
    }
}
