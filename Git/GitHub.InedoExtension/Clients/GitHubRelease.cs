using System.Text.Json.Serialization;

namespace Inedo.Extensions.GitHub.Clients
{
    internal sealed class GitHubRelease
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("tag_name")]
        public string Tag { get; set; }
        [JsonPropertyName("target_commitish")]
        public string Target { get; set; }
        [JsonPropertyName("name")]
        public string Title { get; set; }
        [JsonPropertyName("body")]
        public string Description { get; set; }
        [JsonPropertyName("draft")]
        public bool? Draft { get; set; }
        [JsonPropertyName("prerelease")]
        public bool? Prerelease { get; set; }
        [JsonPropertyName("upload_url")]
        public string UploadUrl { get; set; }
    }
}
