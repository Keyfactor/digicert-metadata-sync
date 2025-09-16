// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DigicertMetadataSync.Models;

namespace DigicertMetadataSync.Logic;

public class Helpers
{
    // Map DigiCert's *string* style (e.g., "text", "int") to Keyfactor enum
    public static KeyfactorMetadataDataType ToKeyfactorDataType(string? dcDataType)
    {
        var t = (dcDataType ?? "text").Trim().ToLowerInvariant();
        return t switch
        {
            "text" or "string" => KeyfactorMetadataDataType.String,
            "int" or "integer" or "number" => KeyfactorMetadataDataType.Integer,
            "date" or "datetime" => KeyfactorMetadataDataType.Date,
            "bool" or "boolean" => KeyfactorMetadataDataType.Boolean,
            // Setting to string due to no way to retrieve options existing.
            "select" or "drop_down_menu" or "picklist"
                or "options" or "choice" => KeyfactorMetadataDataType.String,
            "textarea" or "multiline" => KeyfactorMetadataDataType.BigText,
            "email" or "email_address" or "email_list" => KeyfactorMetadataDataType.Email,
            _ => KeyfactorMetadataDataType.String
        };
    }

    // Map DigiCert's *enum* to Keyfactor enum (used when you lift from account metadata or fields.json)
    public static KeyfactorMetadataDataType ToKeyfactorDataType(DigiCertCustomFieldDataType dc)
    {
        return dc switch
        {
            DigiCertCustomFieldDataType.Anything => KeyfactorMetadataDataType.String,
            DigiCertCustomFieldDataType.Text => KeyfactorMetadataDataType.String,
            DigiCertCustomFieldDataType.Int => KeyfactorMetadataDataType.Integer,
            DigiCertCustomFieldDataType.EmailAddress => KeyfactorMetadataDataType.Email,
            DigiCertCustomFieldDataType.EmailList => KeyfactorMetadataDataType
                .Email, // Keyfactor has single Email data type
            _ => KeyfactorMetadataDataType.String
        };
    }
    /// <summary>
    /// Maps DigiCert wire "data_type" string to DigiCertCustomFieldDataType enum.
    /// Accepts common synonyms and numeric strings; null/empty => Anything.
    /// </summary>
    public static DigiCertCustomFieldDataType ToDigiCertEnumFromString(string? dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType)) return DigiCertCustomFieldDataType.Anything;

        var key = dataType.Trim()
            .Replace("-", "_")
            .Replace(" ", "_")
            .ToLowerInvariant();

        // Numeric string? (rare, but makes config/backfills forgiving)
        if (int.TryParse(key, out var code) &&
            Enum.IsDefined(typeof(DigiCertCustomFieldDataType), code))
        {
            return (DigiCertCustomFieldDataType)code;
        }

        return key switch
        {
            "text" or "string" => DigiCertCustomFieldDataType.Text,
            "int" or "integer" or "number" => DigiCertCustomFieldDataType.Int,
            "email_address" or "email" => DigiCertCustomFieldDataType.EmailAddress,
            "email_list" or "emails" or "email_address_list"
                => DigiCertCustomFieldDataType.EmailList,
            _ => DigiCertCustomFieldDataType.Anything
        };
    }


    // Map Keyfactor → DigiCert (used only as fallback)
    public static DigiCertCustomFieldDataType ToDigiCertDataType(KeyfactorMetadataDataType kf)
    {
        return kf switch
        {
            KeyfactorMetadataDataType.Integer => DigiCertCustomFieldDataType.Int,
            KeyfactorMetadataDataType.Email => DigiCertCustomFieldDataType.EmailAddress,
            // Keyfactor has no "email list" concept; keep it Text unless you specify via fields.json
            KeyfactorMetadataDataType.String => DigiCertCustomFieldDataType.Text,
            KeyfactorMetadataDataType.BigText => DigiCertCustomFieldDataType.Text,
            KeyfactorMetadataDataType.MultipleChoice => DigiCertCustomFieldDataType.Text,
            KeyfactorMetadataDataType.Date => DigiCertCustomFieldDataType.Text,
            KeyfactorMetadataDataType.Boolean => DigiCertCustomFieldDataType.Text,
            _ => DigiCertCustomFieldDataType.Text
        };
    }

    /// <summary>
    ///     Maps Keyfactor's Enrollment (0=Optional, 1=Required, 2=Hidden/Not used)
    ///     into DigiCert's custom-field booleans.
    /// </summary>
    public static (bool IsRequired, bool IsActive) ToDigiCertFlags(int keyfactorEnrollment)
    {
        return keyfactorEnrollment switch
        {
            1 => (true, true), // Required
            2 => (false, false), // Hidden -> make field inactive in DigiCert
            _ => (false, true) // Optional (default)
        };
    }


    // Mirrors: public static JObject Flatten(JObject jObject, string parentName = "")
    public static JsonObject Flatten(JsonObject obj, string parentName = "")
    {
        if (obj is null) throw new ArgumentNullException(nameof(obj));

        var result = new JsonObject();

        void Recurse(JsonNode? node, string prefix)
        {
            switch (node)
            {
                case JsonObject o:
                    foreach (var kvp in o)
                    {
                        var name = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
                        Recurse(kvp.Value, name);
                    }

                    break;

                case JsonArray arr:
                    for (var i = 0; i < arr.Count; i++)
                    {
                        var name = string.IsNullOrEmpty(prefix) ? $"[{i}]" : $"{prefix}[{i}]";
                        Recurse(arr[i], name);
                    }

                    break;

                default:
                    // Leaf (string/number/bool/null). DeepClone to detach from source graph.
                    result[prefix] = node?.DeepClone();
                    break;
            }
        }

        Recurse(obj, parentName ?? string.Empty);
        return result;
    }

    /// <summary>
    ///     Resolves a dot-path on an object graph using reflection and [JsonPropertyName] matches.
    ///     If stringifyLeaf=true, collections/JsonElement/etc. are converted to a scalar string.
    /// </summary>
    public static object? GetPropertyValue(object root, string path, bool stringifyLeaf = true)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or empty.", nameof(path));

        var current = root;

        foreach (var propertyName in path.Split('.',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is null) return null;

            // Support JsonElement containers too
            if (current is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                if (!je.TryGetProperty(propertyName, out je)) return null;
                current = je;
                continue;
            }

            var props = current.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var pi = props.FirstOrDefault(p =>
                string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase) ||
                p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == propertyName);

            if (pi is null) return null;
            current = pi.GetValue(current);
        }

        return stringifyLeaf ? CoerceToScalarString(current) : current;
    }

    /// <summary>
    ///     Convenience: always get a printable scalar string.
    /// </summary>
    public static string? GetPropertyValueAsString(object root, string path)
    {
        return CoerceToScalarString(GetPropertyValue(root, path, false));
    }

    private static string? CoerceToScalarString(object? value)
    {
        if (value is null) return null;

        // Already string
        if (value is string s) return s;

        // JsonElement -> scalar/CSV/JSON
        if (value is JsonElement je) return JsonElementToString(je);

        // Date/Time
        if (value is DateTime dt) return dt.ToString("O", CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dto) return dto.ToString("O", CultureInfo.InvariantCulture);

        // Numbers/booleans/etc.
        if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);

        // IEnumerable -> CSV
        if (value is IEnumerable enumerable && value is not IEnumerable<char>)
        {
            var parts = new List<string>();
            foreach (var item in enumerable)
            {
                var text = CoerceToScalarString(item);
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(text);
            }

            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        // Complex object -> stable JSON snapshot
        return JsonSerializer.Serialize(value);
    }

    private static string? JsonElementToString(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            JsonValueKind.Array => string.Join(", ",
                el.EnumerateArray()
                    .Select(JsonElementToString)
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
            JsonValueKind.Object => el.GetRawText(),
            _ => el.GetRawText()
        };
    }

    public static class DcCustomFieldTypeMapper
    {
        // enum -> wire string
        public static string? ToWireString(DigiCertCustomFieldDataType t)
        {
            return t switch
            {
                DigiCertCustomFieldDataType.Anything => null, // omit to mean "anything"
                DigiCertCustomFieldDataType.Text => "text",
                DigiCertCustomFieldDataType.Int => "int",
                DigiCertCustomFieldDataType.EmailAddress => "email_address",
                DigiCertCustomFieldDataType.EmailList => "email_list",
                _ => null
            };
        }
    }
}