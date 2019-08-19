using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Configurations;
using Newtonsoft.Json.Linq;

namespace Inedo.Extensions.GitLab.Operations
{
    [DisplayName("Ensure GitLab Release")]
    [Description("Sets the release notes for a tag in a GitLab repository.")]
    [Tag("source-control")]
    [ScriptAlias("Ensure-TagReleaseNotes")]
    [ScriptAlias("Ensure-GitLab-Release", Obsolete = true)]
    [ScriptNamespace("GitLab", PreferUnqualified = false)]
    public sealed class GitLabEnsureReleaseOperation : EnsureOperation<GitLabReleaseConfiguration>
    {
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var gitlab = new GitLabClient(this.Template.ApiUrl, this.Template.UserName, this.Template.Password, this.Template.GroupName);

            var tag = await gitlab.GetTagAsync(this.Template.ProjectName, this.Template.Tag, context.CancellationToken).ConfigureAwait(false);

            if (tag == null || !tag.ContainsKey("release") || tag["release"] == null)
            {
                return new GitLabReleaseConfiguration { Exists = false };
            }

            var release = (JObject)tag["release"];
            return new GitLabReleaseConfiguration
            {
                Tag = release.Value<string>("tag_name"),
                Description = release.Value<string>("description")
            };
        }

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
