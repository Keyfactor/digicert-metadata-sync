// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using System.Text.Json.Serialization;

namespace DigicertMetadataSync.Models;

public sealed class DcOrderCertificate
{
    // Core IDs & identifiers
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("thumbprint")] public string? Thumbprint { get; set; }
    [JsonPropertyName("serial_number")] public string? SerialNumber { get; set; }
    [JsonPropertyName("common_name")] public string? CommonName { get; set; }

    // Subject alternative names / S/MIME
    [JsonPropertyName("dns_names")] public List<string>? DnsNames { get; set; }
    [JsonPropertyName("emails")] public List<string>? Emails { get; set; } // S/MIME only

    // Timestamps (API uses ISO 8601 for date_created/issued; yyyy-MM-dd for valid_*).
    // Keep strings to avoid DateOnly converters; parse upstream if you prefer DateOnly/DateTimeOffset.
    [JsonPropertyName("date_created")] public string? DateCreated { get; set; }
    [JsonPropertyName("date_issued")] public string? DateIssued { get; set; }
    [JsonPropertyName("valid_from")] public string? ValidFrom { get; set; } // "YYYY-MM-DD"
    [JsonPropertyName("valid_till")] public string? ValidTill { get; set; } // "YYYY-MM-DD"

    [JsonPropertyName("days_remaining")] public int? DaysRemaining { get; set; }

    // CSR (not returned for VMC)
    [JsonPropertyName("csr")] public string? Csr { get; set; }

    // Organization (on the certificate) & OUs
    [JsonPropertyName("organization")] public DcCertificateOrganizationRef? Organization { get; set; }

    [JsonPropertyName("organization_units")]
    public List<string>? OrganizationUnits { get; set; }

    // Server platform info (TLS)
    [JsonPropertyName("server_platform")] public DcServerPlatform? ServerPlatform { get; set; }

    // Crypto characteristics
    [JsonPropertyName("signature_hash")] public string? SignatureHash { get; set; } // e.g., "sha256"
    [JsonPropertyName("key_size")] public int? KeySize { get; set; }

    // Issuing CA information
    [JsonPropertyName("ca_cert")] public DcCaCertRef? CaCert { get; set; }

    // Validity overrides for the *certificate* (varies by product)
    [JsonPropertyName("cert_validity")] public DcCertValidity? CertValidity { get; set; }

    // Fields that appear in DigiCert’s examples for some products
    [JsonPropertyName("user_id")] public int? UserId { get; set; }

    // DigiCert examples sometimes serialize counts as strings; allow numbers-in-strings.
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("purchased_dns_names")]
    public int? PurchasedDnsNames { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("purchased_wildcard_names")]
    public int? PurchasedWildcardNames { get; set; }

    // Receipt ID appears as string in examples
    [JsonPropertyName("receipt_id")] public string? ReceiptId { get; set; }
}

public sealed class DcCertValidity
{
    [JsonPropertyName("years")] public int? Years { get; set; }
    [JsonPropertyName("days")] public int? Days { get; set; }

    // DigiCert documents custom expiration as a string in requests (e.g., "09 JUN 2025");
    // responses may include it when used. Keep it as string for maximum compatibility.
    [JsonPropertyName("custom_expiration_date")]
    public string? CustomExpirationDate { get; set; }
}

public sealed class DcCertificateOrganizationRef
{
    [JsonPropertyName("id")] public int? Id { get; set; }
}

public sealed class DcServerPlatform
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("install_url")] public string? InstallUrl { get; set; }
    [JsonPropertyName("csr_url")] public string? CsrUrl { get; set; }
}

public sealed class DcCaCertRef
{
    // DigiCert shows this as a string token (e.g., "DF3689F672CCB90C")
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}