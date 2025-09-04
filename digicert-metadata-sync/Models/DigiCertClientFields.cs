// in Models/DigicertMetadataFieldCreate.cs

using Newtonsoft.Json;

namespace DigicertMetadataSync.Models;

public sealed class DigicertMetadataFieldCreate
{
    [JsonProperty("label")] public string Label { get; set; } = "";
    [JsonProperty("data_type")] public string DataType { get; set; } = "text"; // adjust as needed
    [JsonProperty("is_required")] public bool IsRequired { get; set; }
    [JsonProperty("is_active")] public bool IsActive { get; set; } = true;
}

public sealed class DigicertMetadataFieldUpdate
{
    [JsonProperty("label")] public string? Label { get; set; }
    [JsonProperty("data_type")] public string? DataType { get; set; }
    [JsonProperty("is_required")] public bool? IsRequired { get; set; }
    [JsonProperty("is_active")] public bool? IsActive { get; set; }
}

public sealed class CustomDigicertMetadataInstance
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("label")] public string Label { get; set; } = "";
    [JsonProperty("is_required")] public bool IsRequired { get; set; }
    [JsonProperty("is_active")] public bool IsActive { get; set; }

    [JsonProperty("data_type")] public string DataType { get; set; } = "";
    // Add any extra properties you use (e.g., kf_field_name) with proper JsonProperty
}

public sealed class DigicertMetadataUpdateInstance
{
    [JsonProperty("metadata_id")] public int MetadataId { get; set; }
    [JsonProperty("value")] public string Value { get; set; } = "";
}