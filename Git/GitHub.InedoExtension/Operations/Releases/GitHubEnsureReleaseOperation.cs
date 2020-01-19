﻿using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Configurations;

namespace Inedo.Extensions.GitHub.Operations.Releases
{
    [DisplayName("Ensure GitHub Release")]
    [Description("Creates or updates a tagged release in a GitHub repository.")]
    [Tag("source-control")]
    [ScriptAlias("Ensure-Release")]
    [ScriptAlias("Ensure-GitHub-Release", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    public sealed class GitHubEnsureReleaseOperation : EnsureOperation<GitHubReleaseConfiguration>
    {
        public override async Task<PersistedConfiguration> CollectAsync(IOperationCollectionContext context)
        {
            var (credentials, resource) = this.Template.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var github = new GitHubClient(credentials, resource);

            var ownerName = AH.CoalesceString(resource.OrganizationName, credentials.UserName);

            var release = await github.GetReleaseAsync(ownerName, resource.RepositoryName, this.Template.Tag, context.CancellationToken);

            if (release == null)
            {
                return new GitHubReleaseConfiguration { Exists = false };
            }

            return new GitHubReleaseConfiguration
            {
                Tag = (string)release["tag_name"],
                Target = (string)release["target_commitish"],
                Title = (string)release["name"],
                Description = (string)release["body"],
                Draft = (bool)release["draft"],
                Prerelease = (bool)release["prerelease"]
            };
        }

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            var (credentials, resource) = this.Template.GetCredentialsAndResource(context as ICredentialResolutionContext);
            var github = new GitHubClient(credentials, resource);

            var ownerName = AH.CoalesceString(resource.OrganizationName, credentials.UserName);

            await github.EnsureReleaseAsync(ownerName, resource.RepositoryName, this.Template.Tag, this.Template.Target, this.Template.Title, this.Template.Description, this.Template.Draft, this.Template.Prerelease, context.CancellationToken).ConfigureAwait(false);
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Ensure GitHub Release"),
               new RichDescription("in ", new Hilite(config.DescribeSource()), " for tag ", new Hilite(config[nameof(this.Template.Tag)]))
            );
        }
    }
}
