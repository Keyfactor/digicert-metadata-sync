// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using System.Text.RegularExpressions;
using DigicertMetadataSync.Models;

namespace DigicertMetadataSync.Logic;

public class ValueCoercionDC
{
    private static readonly Regex EmailRx = new(@"^[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? CoerceForDigiCert(string? value,
        DigiCertCustomFieldDataType dcType,
        string[]? kfOptions = null)
    {
        if (value is null) return null;

        // Common normalize
        var s = value.Trim();
        if (s.Length == 0) return null;

        switch (dcType)
        {
            case DigiCertCustomFieldDataType.Int:
                // Accept int-like strings only
                return int.TryParse(s, out var n) ? n.ToString() : null;

            case DigiCertCustomFieldDataType.EmailAddress:
                return EmailRx.IsMatch(s) ? s : null;

            case DigiCertCustomFieldDataType.EmailList:
            {
                // split on , ; whitespace, validate each, return CSV (", ")
                var emails = s.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => e.Trim().Trim('"', '\'', '<', '>', '(', ')', '[', ']'))
                    .Where(e => EmailRx.IsMatch(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return emails.Length == 0 ? null : string.Join(",", emails);
            }

            case DigiCertCustomFieldDataType.Text:
                // If KF field was MultipleChoice and you provided options, normalize to the canonical option text
                if (kfOptions is { Length: > 0 })
                {
                    var norm = NormalizeChoice(s);
                    var match = kfOptions.FirstOrDefault(o => NormalizeChoice(o) == norm);
                    return match ?? s; // fall back to raw text if not matched
                }

                return s;

            case DigiCertCustomFieldDataType.Anything:
                // Pass through; DigiCert treats omitted data_type as free text
                return s;

            default:
                return s;
        }
    }

    private static string NormalizeChoice(string x)
    {
        return Regex.Replace(x.Trim(), @"\s+", " ").ToLowerInvariant();
    }
}