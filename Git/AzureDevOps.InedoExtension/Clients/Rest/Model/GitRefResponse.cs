namespace Inedo.Extensions.AzureDevOps.VisualStudioOnline.Model
{
    public class GitRefResponse
    {
        public GitRef[] value { get; set; }
        public int count { get; set; }
    }
       public class GitRef
    {
        public string name { get; set; }
        public string branchName { get { return this.name?.Replace("refs/heads/", string.Empty); } }
        public string objectId { get; set; }
        public string url { get; set; }
    }
}
