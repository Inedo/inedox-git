using System.Text.Json.Serialization;

namespace Inedo.Extensions.GitHub.Clients
{
    internal sealed class GitHubProject
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("columns_url")]
        public string ColumnsUrl { get; set; }
    }
}
