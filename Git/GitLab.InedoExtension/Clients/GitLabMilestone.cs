namespace Inedo.Extensions.GitLab.Clients
{
    public sealed record GitLabMilestone(long Id, string Title, string Description = null, string StartDate = null, string DueDate = null, string State = null);
}
