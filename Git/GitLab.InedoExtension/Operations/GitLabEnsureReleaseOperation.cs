using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Configurations;

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
            var (credentials, resource) = this.Template.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var gitlab = new GitLabClient(credentials);

            var tag = await gitlab.GetTagAsync(resource, this.Template.Tag, context.CancellationToken).ConfigureAwait(false);
            if (tag == null)
                return new GitLabReleaseConfiguration { Exists = false };

            return new GitLabReleaseConfiguration
            {
                Tag = tag.Name,
                Description = tag.Description
            };
        }

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.Template.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var gitlab = new GitLabClient(credentials);

            await gitlab.EnsureReleaseAsync(resource, this.Template.Tag, this.Template.Description ?? string.Empty, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Ensure GitLab Release"),
               new RichDescription("in ", new Hilite(config.DescribeSource()), " for tag ", new Hilite(config[nameof(this.Template.Tag)]))
            );
        }
    }
}
