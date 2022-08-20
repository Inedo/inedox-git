using System.Text.Json.Serialization;

namespace Inedo.Extensions.GitHub.Clients
{
    internal sealed class GitHubMilestone
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("due_on")]
        public string DueOn { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
    }
}
