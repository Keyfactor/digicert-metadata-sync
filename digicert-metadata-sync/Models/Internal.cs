// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace DigicertMetadataSync.Models;

/// <summary>
///     Represents a unified format for metadata fields.
/// </summary>
public class UnifiedFormatField
{
    private DigiCertCustomFieldDataType _dcType = DigiCertCustomFieldDataType.Anything;
    private KeyfactorMetadataDataType _kfType = KeyfactorMetadataDataType.String;
    public int DigiCertMetadaFieldId = 0; // Default to 0 (not set)
    public string DigicertFieldName { get; set; } = string.Empty;
    public string KeyfactorMetadataFieldName { get; set; } = string.Empty;

    public string KeyfactorDescription { get; set; } = string.Empty;

    // Bind integer codes from fields.json
    // "keyfactorDataType": 2  -> KeyfactorMetadataDataType.Integer
    // ---- enum properties you should use everywhere in code ----
    [ConfigurationKeyName("keyfactorDataType")]
    [JsonPropertyName("keyfactorDataType")]
    public KeyfactorMetadataDataType KeyfactorDataType
    {
        get => _kfType;
        set => _kfType = Enum.IsDefined(typeof(KeyfactorMetadataDataType), value)
            ? value
            : KeyfactorMetadataDataType.String; // safe default when 0/unknown comes in
    }

    [ConfigurationKeyName("digicertCustomFieldDataType")]
    [JsonPropertyName("digicertCustomFieldDataType")]
    public DigiCertCustomFieldDataType DigicertDataType
    {
        get => _dcType;
        set => _dcType = Enum.IsDefined(typeof(DigiCertCustomFieldDataType), value)
            ? value
            : DigiCertCustomFieldDataType.Anything; // omit on wire by default
    }

    public string? KeyfactorHint { get; set; }
    public string? KeyfactorValidation { get; set; }
    public int KeyfactorEnrollment { get; set; } // Default to Optional
    public string? KeyfactorMessage { get; set; }
    public string[]? KeyfactorOptions { get; set; } // Added for options mapping
    public string? KeyfactorDefaultValue { get; set; }
    public int KeyfactorDisplayOrder { get; set; }
    public bool KeyfactorCaseSensitive { get; set; } = false; // Default to false
    public int KeyfactorMetadataFieldId { get; set; } // Default to 0 (not set)
    public bool KeyfactorAllowAPI { get; set; } = true;
    public UnifiedFieldType ToolFieldType { get; set; } = UnifiedFieldType.Custom; // Default to Custom
}

/// <summary>
///     Used for storage of replacement characters during field conversion.
/// </summary>
public class CharDBItem
{
    public string character { get; set; } = string.Empty;
    public string replacementcharacter { get; set; } = string.Empty;
}

public enum UnifiedFieldType
{
    Manual = 1,
    Custom = 2
}