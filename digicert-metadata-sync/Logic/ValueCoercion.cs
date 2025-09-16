// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DigicertMetadataSync.Models;

public static class ValueCoercion
{
    // --- ADD: configurable Keyfactor date format (default keeps current behavior) ---
    private const string DefaultKfDateFormat = "yyyy-MM-dd";
    private static string _kfDateFormat = DefaultKfDateFormat;
    /// <summary>
    /// Output format for Keyfactor Date metadata (e.g., "M/d/yyyy h:mm:ss tt").
    /// Set this once at startup from config. Null/empty resets to default "yyyy-MM-dd".
    /// </summary>
    public static string KeyfactorDateFormat
    {
        get => _kfDateFormat;
        set => _kfDateFormat = string.IsNullOrWhiteSpace(value) ? DefaultKfDateFormat : value!;
    }

    private static readonly Regex EmailRx = new(@"^[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // ---- public API ----

    public static object? Coerce(JsonElement value,
        KeyfactorMetadataDataType type,
        string[]? options)
    {
        return type switch
        {
            KeyfactorMetadataDataType.Integer => CoerceInt(value),
            KeyfactorMetadataDataType.Boolean => CoerceBool(value),
            KeyfactorMetadataDataType.Date => CoerceDateYmd(value),
            KeyfactorMetadataDataType.Email => CoerceEmailCsv(value),
            KeyfactorMetadataDataType.MultipleChoice => CoerceChoice(value, options),
            KeyfactorMetadataDataType.BigText => CoerceString(value, true),
            KeyfactorMetadataDataType.String => CoerceString(value),
            _ => CoerceString(value)
        };
    }

    // Back-compat overload: accepts numeric code (e.g., field.KeyfactorDataTypeCode)
    public static object? Coerce(JsonElement value,
        int keyfactorTypeCode,
        string[]? options)
    {
        var type = Enum.IsDefined(typeof(KeyfactorMetadataDataType), keyfactorTypeCode)
            ? (KeyfactorMetadataDataType)keyfactorTypeCode
            : KeyfactorMetadataDataType.String;

        return Coerce(value, type, options);
    }

    // ---- scalars ----

    private static int? CoerceInt(JsonElement v)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.Number:
                if (v.TryGetInt32(out var n)) return n;
                if (v.TryGetInt64(out var l))
                {
                    if (l > int.MaxValue || l < int.MinValue) return null;
                    return (int)l;
                }

                return null;

            case JsonValueKind.String:
                var s = v.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return i;
                return null;

            case JsonValueKind.True: return 1;
            case JsonValueKind.False: return 0;
            default: return null;
        }
    }

    private static bool? CoerceBool(JsonElement v)
    {
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.TryGetInt32(out var n) ? n != 0 : null,
            JsonValueKind.String => ParseBoolLoose(v.GetString()),
            _ => null
        };
    }

    private static string? CoerceDateYmd(JsonElement v)
    {
        // Accept ISO 8601 or y/M/d etc., emit in configurable Keyfactor format
        if (TryExtractString(v, out var s) && !string.IsNullOrWhiteSpace(s))
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dto))
                return dto.ToString(_kfDateFormat, CultureInfo.InvariantCulture);

        // If it's already yyyy-MM-dd but config requests a different format, reformat
        if (v.ValueKind == JsonValueKind.String)
        {
            var raw = v.GetString();
            if (!string.IsNullOrWhiteSpace(raw) && Regex.IsMatch(raw!, @"^\d{4}-\d{2}-\d{2}$"))
            {
                if (_kfDateFormat == DefaultKfDateFormat)
                    return raw;

                if (DateTime.TryParseExact(raw, DefaultKfDateFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return dt.ToString(_kfDateFormat, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static string? CoerceString(JsonElement v, bool multiline = false)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.String:
                var s = v.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return v.GetRawText(); // culture-invariant

            case JsonValueKind.Array:
                // join array items with ", " for display fields
                var parts = v.EnumerateArray()
                    .Select(e => CoerceString(e))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToArray();
                return parts.Length == 0 ? null : string.Join(", ", parts);

            case JsonValueKind.Object:
                // For BigText allow JSON snapshot if that's what you want to store
                return multiline ? v.GetRawText() : null;

            default:
                return null;
        }
    }

    // ---- special types ----

    private static string? CoerceEmailCsv(JsonElement v)
    {
        var all = ExtractEmails(v);
        if (all.Count == 0) return null;

        var normalized = all.Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(", ", normalized);
    }

    private static object? CoerceChoice(JsonElement v, string[]? options)
    {
        // Keyfactor MultipleChoice expects a single selected option (string).
        var val = CoerceString(v);
        if (string.IsNullOrWhiteSpace(val))
            return null;

        if (options is null || options.Length == 0)
            return val; // nothing to validate against

        // Normalize then match case-insensitively
        var norm = NormalizeChoice(val);
        var match = options.FirstOrDefault(o => NormalizeChoice(o) == norm);
        return match ?? null; // invalid value -> null so you don't submit a bad choice
    }

    // ---- helpers ----

    private static bool? ParseBoolLoose(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().ToLowerInvariant();
        return s switch
        {
            "true" or "t" or "yes" or "y" or "1" => true,
            "false" or "f" or "no" or "n" or "0" => false,
            _ => null
        };
    }

    private static bool TryExtractString(JsonElement v, out string? s)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.String:
                s = v.GetString();
                return true;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                s = v.GetRawText();
                return true;
            default:
                s = null;
                return false;
        }
    }

    private static string NormalizeChoice(string s)
    {
        return Regex.Replace(s.Trim(), @"\s+", " ").ToLowerInvariant();
    }

    private static List<string> ExtractEmails(JsonElement v)
    {
        var outList = new List<string>(8);

        void FromText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var pieces = text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in pieces)
            {
                var candidate = raw.Trim().Trim('"', '\'', '<', '>', '(', ')', '[', ']');
                if (EmailRx.IsMatch(candidate))
                    outList.Add(candidate);
            }
        }

        switch (v.ValueKind)
        {
            case JsonValueKind.String:
                FromText(v.GetString());
                break;

            case JsonValueKind.Array:
                foreach (var item in v.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String) FromText(item.GetString());
                    else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("email", out var one) &&
                             one.ValueKind == JsonValueKind.String)
                        FromText(one.GetString());
                break;

            case JsonValueKind.Object:
                if (v.TryGetProperty("email", out var e) && e.ValueKind == JsonValueKind.String)
                    FromText(e.GetString());
                else if (v.TryGetProperty("emails", out var es)) outList.AddRange(ExtractEmails(es));
                else FromText(v.GetRawText());
                break;

            default:
                FromText(v.GetRawText());
                break;
        }

        return outList;
    }
}