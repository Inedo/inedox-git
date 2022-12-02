using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

#nullable enable

namespace Inedo.Extensions.AzureDevOps.Client
{
    [JsonSerializable(typeof(AdoList<AdoProject>))]
    [JsonSerializable(typeof(AdoList<AdoRepo>))]
    [JsonSerializable(typeof(AdoList<AdoBuildDef>))]
    [JsonSerializable(typeof(AdoList<AdoGitRef>))]
    [JsonSerializable(typeof(AdoList<AdoBuild>))]
    [JsonSerializable(typeof(AdoList<AdoPullRequest>))]
    [JsonSerializable(typeof(AdoList<AdoArtifact>))]
    [JsonSerializable(typeof(AdoList<AdoIteration>))]
    [JsonSerializable(typeof(AdoWorkItemQueryResult))]
    [JsonSerializable(typeof(AdoList<AdoWorkItem>))]
    [JsonSerializable(typeof(AdoQuery))]
    [JsonSerializable(typeof(IEnumerable<AdoCreateWorkItem>))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    internal partial class AzureDevOpsJsonContext : JsonSerializerContext
    {
    }

    internal sealed class AdoList<T> where T : class, new()
    {
        public int? Count { get; set; }
        public T[]? Value { get; set; }
    }

    internal sealed class AdoProject
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
    }

    internal sealed class AdoRepo
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? DefaultBranch { get; set; }
        public string? RemoteUrl { get; set; }
        [JsonPropertyName("_links")]
        public AdoRepoLinks? Links { get; set; }
    }

    internal sealed class AdoRepoLinks
    {
        public AdoLink? Web { get; set; }
    }

    internal sealed class AdoBuild
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }
        public string? BuildNumber { get; set; }
        public AdoBuildDef? Definition { get; set; }
        public string? Status { get; set; }
        public string? Result { get; set; }
    }

    internal sealed class AdoBuildDef
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    internal sealed class AdoGitRef
    {
        public bool IsLocked { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        public string? ObjectId { get; set; }
    }

    internal sealed class AdoPullRequest
    {
        [JsonPropertyName("pullRequestId")]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Status { get; set; }
        public string? SourceRefName { get; set; }
        public string? TargetRefName { get; set; }
    }

    internal sealed class AdoArtifactResource
    {
        public string? DownloadUrl { get; set; }
    }

    internal sealed class AdoArtifact
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public AdoArtifactResource? Resource { get; set; }
    }

    internal sealed class AdoIteration
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
    }

    internal sealed class AdoWorkItemQueryResult
    {
        public AdoFieldRef[]? Columns { get; set; }
        public AdoWorkItemReference[]? WorkItems { get; set; }
    }

    internal sealed class AdoWorkItemReference
    {
        public int Id { get; set; }
    }

    internal sealed class AdoWorkItem
    {
        public int Id { get; set; }
        public int Rev { get; set; }
        public Dictionary<string, JsonValue>? Fields { get; set; }
        public string? Url { get; set; }
        [JsonPropertyName("_links")]
        public AdoWorkItemLinks? Links { get; set; }
    }

    internal sealed class AdoWorkItemLinks
    {
        public AdoLink? Html { get; set; }
    }

    internal sealed class AdoLink
    {
        public string? Href { get; set; }
    }

    internal sealed class AdoQuery
    {
        public string? Query { get; set; }
    }

    internal sealed class AdoFieldRef
    {
        public string? Name { get; set; }
        public string? ReferenceName { get; set; }
    }

    internal sealed class AdoCreateWorkItem
    {
        public string? From { get; set; }
        [JsonPropertyName("op")]
        public string? Operation { get; set; }
        public string? Path { get; set; }
        public JsonValue? Value { get; set; }
    }
}
