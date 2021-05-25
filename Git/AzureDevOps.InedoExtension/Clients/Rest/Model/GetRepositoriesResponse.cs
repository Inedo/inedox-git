namespace Inedo.Extensions.AzureDevOps.VisualStudioOnline.Model
{
    public class GitRepositoryResponse
    {
        public GitRepository[] value { get; set; }
        public int count { get; set; }

    }

    public class GitRepository
    {
        public string defaultBranch { get; set; }
        public string id { get; set; }
        public bool isFork { get; set; }
        public string name { get; set; }
        public GitRepositoryRef parentRepository { get; set; }
        public Project project { get; set; }
        public string remoteUrl { get; set; }
        public int size { get; set; }
        public string url { get; set; }
        public string sshUrl { get; set; }
        public string[] validRemoteUrls { get; set; }
        public string webUrl { get; set; }
    }

    public class GitRepositoryRef
    {
        public string id { get; set; }
        public bool isFork { get; set; }
        public string name { get; set; }
        public Project project { get; set; }
        public string remoteUrl { get; set; }
        public string url { get; set; }
        public string sshUrl { get; set; }

    }
}
