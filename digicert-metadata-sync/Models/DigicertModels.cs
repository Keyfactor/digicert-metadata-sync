// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
using System.Text.Json;
using System.Text.Json.Serialization;
using static DigicertMetadataSync.Logic.Helpers;

namespace DigicertMetadataSync.Models;

// ---- Account/metadata ----
public sealed class DcCustomFieldListRoot
{
    [JsonPropertyName("metadata")] public List<DcCustomField>? Metadata { get; set; }
}

public sealed class DcCustomField
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("is_required")] public bool? IsRequired { get; set; }
    [JsonPropertyName("is_active")] public bool? IsActive { get; set; }
    [JsonPropertyName("data_type")] public string? DataType { get; set; } = "_"; // text, int, email_address, email_list, etc.
    [JsonPropertyName("show_in_receipt")] public bool? ShowInReceipt { get; set; }
}

public sealed class DcCustomFieldCreate
{
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;

    [JsonPropertyName("is_required")] public bool? IsRequired { get; set; }

    [JsonPropertyName("is_active")] public bool? IsActive { get; set; }

    // Enum for internal use
    [JsonIgnore] public DigiCertCustomFieldDataType DataTypeEnum { get; set; } = DigiCertCustomFieldDataType.Anything;

    // Wire property (DigiCert expects string or omitted)
    [JsonPropertyName("data_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataType
    {
        get => DcCustomFieldTypeMapper.ToWireString(DataTypeEnum);
        set
        {
            // optional tolerant parsing if you ever read it back
            DataTypeEnum = value?.ToLowerInvariant() switch
            {
                "text" => DigiCertCustomFieldDataType.Text,
                "int" => DigiCertCustomFieldDataType.Int,
                "email_address" => DigiCertCustomFieldDataType.EmailAddress,
                "email_list" => DigiCertCustomFieldDataType.EmailList,
                _ => DigiCertCustomFieldDataType.Anything
            };
        }
    }

    [JsonPropertyName("show_in_receipt")] public bool? ShowInReceipt { get; set; } = true;
}

public sealed class DcCustomFieldValueUpdate
{
    [JsonPropertyName("metadata_id")] public int MetadataId { get; set; }
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
}

public sealed class DcCustomFieldUpdate
{
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("is_required")] public bool? IsRequired { get; set; }
    [JsonPropertyName("is_active")] public bool? IsActive { get; set; }
    [JsonPropertyName("data_type")] public string? DataType { get; set; }
    [JsonPropertyName("show_in_receipt")] public bool? ShowInReceipt { get; set; }
}

// ---- Orders ----
public sealed class DcOrderList
{
    [JsonPropertyName("orders")] public List<DcOrderListItem>? Orders { get; set; }
    [JsonPropertyName("page")] public DcPage? Page { get; set; }
}

public sealed class DcPage
{
    [JsonPropertyName("total")] public int? Total { get; set; }
    [JsonPropertyName("limit")] public int? Limit { get; set; }
    [JsonPropertyName("offset")] public int? Offset { get; set; }
}

public sealed class DcOrderListItem
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("certificate")] public DcOrderCertificateSummary? Certificate { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("date_created")] public string? DateCreated { get; set; }
    [JsonPropertyName("product_name_id")] public string? ProductNameId { get; set; }
    [JsonPropertyName("has_duplicates")] public bool? HasDuplicates { get; set; }
    [JsonPropertyName("duplicates_count")] public int? DuplicatesCount { get; set; }
    [JsonPropertyName("reissues_count")] public int? ReissuesCount { get; set; }
}

public sealed class DcOrderCertificateSummary
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("common_name")] public string? CommonName { get; set; }
    [JsonPropertyName("serial_number")] public string? SerialNumber { get; set; }
    [JsonPropertyName("thumbprint")] public string? Thumbprint { get; set; }
}

public sealed class DcOrderInfo
{
    // Core
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }

    // Existing model (your class)
    [JsonPropertyName("certificate")] public DcOrderCertificate? Certificate { get; set; }

    // Contacts / org / container
    [JsonPropertyName("organization")] public DcOrderOrganization? Organization { get; set; }

    [JsonPropertyName("organization_contact")]
    public DcOrderContact? OrganizationContact { get; set; }

    [JsonPropertyName("technical_contact")]
    public DcOrderContact? TechnicalContact { get; set; }

    [JsonPropertyName("verified_contacts")]
    public List<DcOrderContact>? VerifiedContacts { get; set; } // EV & others

    [JsonPropertyName("container")] public DcOrderContainer? Container { get; set; }

    // Product block
    [JsonPropertyName("product")] public DcOrderProduct? Product { get; set; }
    [JsonPropertyName("product_name_id")] public string? ProductNameId { get; set; } // convenience alias

    // Emails & custom fields
    [JsonPropertyName("additional_emails")]
    public List<string>? AdditionalEmails { get; set; }

    [JsonPropertyName("custom_fields")] public List<DcOrderCustomField>? CustomFields { get; set; }

    // Pricing & payment
    [JsonPropertyName("price")] public decimal? Price { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("prices")] public List<DcOrderPrice>? Prices { get; set; } // reissue totals by currency
    [JsonPropertyName("payment_method")] public string? PaymentMethod { get; set; } // balance|card|subscription
    [JsonPropertyName("payment_profile")] public DcPaymentProfile? PaymentProfile { get; set; }

    [JsonPropertyName("is_out_of_contract")]
    public bool? IsOutOfContract { get; set; }

    // Order features / toggles
    [JsonPropertyName("disable_issuance_email")]
    public bool? DisableIssuanceEmail { get; set; }

    [JsonPropertyName("disable_ct")] public bool? DisableCt { get; set; }
    [JsonPropertyName("allow_duplicates")] public bool? AllowDuplicates { get; set; }
    [JsonPropertyName("duplicates_count")] public int? DuplicatesCount { get; set; }
    [JsonPropertyName("reissues_count")] public int? ReissuesCount { get; set; }
    [JsonPropertyName("server_licenses")] public int? ServerLicenses { get; set; }

    [JsonPropertyName("is_guest_access_enabled")]
    public bool? IsGuestAccessEnabled { get; set; }

    [JsonPropertyName("has_pending_request")]
    public bool? HasPendingRequest { get; set; }

    // Renewals / multi-year plan
    [JsonPropertyName("is_renewal")] public bool? IsRenewal { get; set; }
    [JsonPropertyName("renewed_order_id")] public int? RenewedOrderId { get; set; }

    [JsonPropertyName("renewal_of_order_id")]
    public int? RenewalOfOrderId { get; set; }

    // DigiCert returns "1" when true (omitted otherwise). Keep as string to match raw.
    [JsonPropertyName("is_multi_year_plan")]
    public string? IsMultiYearPlan { get; set; }

    // Approvals & assignments
    [JsonPropertyName("order_approval_complete")]
    public bool? OrderApprovalComplete { get; set; } // EV TLS/SSL

    [JsonPropertyName("approver")] public DcUserDetails? Approver { get; set; } // the user who approved
    [JsonPropertyName("user_assignments")] public List<DcUserDetails>? UserAssignments { get; set; }

    // API key provenance
    [JsonPropertyName("api_key")] public DcOrderApiKey? ApiKey { get; set; }

    // Competitive replacement & benefits
    [JsonPropertyName("benefits")] public DcOrderBenefits? Benefits { get; set; }

    // Alternative identifiers
    [JsonPropertyName("alternative_order_id")]
    public string? AlternativeOrderId { get; set; }

    // VMC / CMC (only for those products)
    [JsonPropertyName("vmc")] public DcOrderVmc? Vmc { get; set; }

    // Verification rollup (statuses vary by product — strings such as pending/complete)
    [JsonPropertyName("verification")] public DcOrderVerification? Verification { get; set; }

    // Requests history summary (reissues/duplicates/etc.) — schema varies; keep minimal
    [JsonPropertyName("requests")] public List<DcOrderRequestSummary>? Requests { get; set; }

    // Future-proofing: capture anything DigiCert adds later
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

// -------- nested models --------

public sealed class DcOrderProduct
{
    [JsonPropertyName("name_id")] public string? NameId { get; set; } // e.g., ssl_basic, code_signing_ev, vmc_basic
    [JsonPropertyName("type_hint")] public string? TypeHint { get; set; } // sometimes present
    [JsonPropertyName("type_id")] public int? TypeId { get; set; }
    [JsonPropertyName("brand")] public string? Brand { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcOrderContainer
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcOrderOrganization
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("assumed_name")] public string? AssumedName { get; set; } // DBA
    [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
    [JsonPropertyName("address")] public DcOrgAddress? Address { get; set; }
    [JsonPropertyName("telephone")] public string? Telephone { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcOrgAddress
{
    [JsonPropertyName("street_address")] public string? StreetAddress { get; set; }
    [JsonPropertyName("locality")] public string? Locality { get; set; }
    [JsonPropertyName("state")] public string? State { get; set; }
    [JsonPropertyName("postal_code")] public string? PostalCode { get; set; }
    [JsonPropertyName("country")] public string? Country { get; set; }
}

public sealed class DcOrderContact
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("telephone")] public string? Telephone { get; set; }
    [JsonPropertyName("job_title")] public string? JobTitle { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcUserDetails
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcOrderApiKey
{
    [JsonPropertyName("name")] public string? Name { get; set; } // API key name or ACME Directory URL
    [JsonPropertyName("key_type")] public string? KeyType { get; set; } // api_key | acme_url
}

public sealed class DcOrderBenefits
{
    [JsonPropertyName("actual_price")] public string? ActualPrice { get; set; }
    [JsonPropertyName("discount_percent")] public string? DiscountPercent { get; set; }
    [JsonPropertyName("benefits")] public List<string>? BenefitTypes { get; set; } // e.g., cr_benefit
    [JsonPropertyName("benefits_data")] public DcOrderBenefitsData? BenefitsData { get; set; }
}

public sealed class DcOrderBenefitsData
{
    [JsonPropertyName("cr_benefit")] public DcCompetitiveReplacement? CompetitiveReplacement { get; set; }
}

public sealed class DcCompetitiveReplacement
{
    [JsonPropertyName("type")] public string? Type { get; set; } // DISCOUNT

    [JsonPropertyName("discount_percentage")]
    public float? DiscountPercentage { get; set; }

    [JsonPropertyName("premium_discount_percentage")]
    public float? PremiumDiscountPercentage { get; set; }

    [JsonPropertyName("availed_domains")] public List<string>? AvailedDomains { get; set; }
}

public sealed class DcOrderPrice
{
    [JsonPropertyName("price")] public decimal? Value { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
}

public sealed class DcPaymentProfile
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("card_type")] public string? CardType { get; set; }
    [JsonPropertyName("masked_number")] public string? MaskedNumber { get; set; }
    [JsonPropertyName("exp_month")] public int? ExpMonth { get; set; }
    [JsonPropertyName("exp_year")] public int? ExpYear { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcOrderVmc
{
    [JsonPropertyName("logo_id")] public int? LogoId { get; set; }
    [JsonPropertyName("logo")] public string? LogoBase64 { get; set; }
    [JsonPropertyName("enable_hosting")] public bool? EnableHosting { get; set; }

    [JsonPropertyName("hosted_logo_location")]
    public string? HostedLogoLocation { get; set; }

    [JsonPropertyName("hosted_cert_location")]
    public string? HostedCertLocation { get; set; }

    [JsonPropertyName("mark_type")] public string? MarkType { get; set; } // registered_mark | government_mark
    [JsonPropertyName("mark_type_data")] public DcVmcMarkTypeData? MarkTypeData { get; set; }
}

public sealed class DcVmcMarkTypeData
{
    [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
    [JsonPropertyName("state_province")] public string? StateProvince { get; set; }
    [JsonPropertyName("locality")] public string? Locality { get; set; }
}

// Verification rollup (statuses are strings like "pending", "complete"; presence varies by product)
public sealed class DcOrderVerification
{
    [JsonPropertyName("organization_type")]
    public string? OrganizationType { get; set; }

    [JsonPropertyName("organization_status")]
    public string? OrganizationStatus { get; set; }

    [JsonPropertyName("address_verification")]
    public string? AddressVerification { get; set; }

    [JsonPropertyName("blacklist_fraud")] public string? BlacklistFraud { get; set; }

    [JsonPropertyName("blacklist_fraud_malware")]
    public string? BlacklistFraudMalware { get; set; }

    [JsonPropertyName("request_authenticity")]
    public string? RequestAuthenticity { get; set; }

    [JsonPropertyName("operational_existence")]
    public string? OperationalExistence { get; set; }

    [JsonPropertyName("place_of_business_verification")]
    public string? PlaceOfBusinessVerification { get; set; }

    [JsonPropertyName("phone_number_verification")]
    public string? PhoneNumberVerification { get; set; }

    [JsonPropertyName("approver_verification")]
    public string? ApproverVerification { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcOrderRequestSummary
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; } // duplicate | reissue | renewal | etc.
    [JsonPropertyName("status")] public string? Status { get; set; } // pending | complete | etc.
    [JsonPropertyName("date_created")] public string? DateCreated { get; set; } // ISO-8601
    [JsonExtensionData] public Dictionary<string, JsonElement>? Ext { get; set; }
}

public sealed class DcOrderCustomField
{
    [JsonPropertyName("id")] public int? Id { get; set; }
    [JsonPropertyName("metadata_id")] public int? MetadataId { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("value")] public JsonElement Value { get; set; }
}

public sealed class DcOrderCustomFieldValue
{
    [JsonPropertyName("metadata_id")] public int MetadataId { get; set; }
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
}
// ---------------------- Minimal helper types you can add ----------------------

public sealed class ReportsQueryRequest
{
    [JsonPropertyName("query")] public string Query { get; set; } = string.Empty;
    [JsonPropertyName("variables")] public ReportsQueryVars Variables { get; set; } = new();
}

public sealed class ReportsQueryVars
{
    // `t` matches $t in the GraphQL query
    [JsonPropertyName("t")] public string T { get; set; } = string.Empty;
}

public sealed class ReportsQueryResponse<TData>
{
    [JsonPropertyName("data")] public TData? Data { get; set; }
    [JsonPropertyName("errors")] public object? Errors { get; set; } // ignored but useful to log if needed
}

public sealed class ReportsOrderDetailsData
{
    [JsonPropertyName("order_details")] public List<ReportsOrderId>? OrderDetails { get; set; }
}

public sealed class ReportsOrderId
{
    [JsonPropertyName("id")] public string? Id { get; set; }
}

public enum DigiCertCustomFieldDataType
{
    Anything = 0,
    Text = 1,
    Int = 2,
    EmailAddress = 3,
    EmailList = 4
}

public static class DigiCertFieldTypeWire
{
    // enum -> wire string (null = omit field)
    public static string? ToWireString(this DigiCertCustomFieldDataType t)
    {
        return t switch
        {
            DigiCertCustomFieldDataType.Anything => null, // omit per DigiCert spec
            DigiCertCustomFieldDataType.Text => "text",
            DigiCertCustomFieldDataType.Int => "int",
            DigiCertCustomFieldDataType.EmailAddress => "email_address",
            DigiCertCustomFieldDataType.EmailList => "email_list",
            _ => null
        };
    }

    // optional: wire string -> enum
    public static DigiCertCustomFieldDataType Parse(string? s)
    {
        return (s ?? "").Trim().ToLowerInvariant() switch
        {
            "text" => DigiCertCustomFieldDataType.Text,
            "int" => DigiCertCustomFieldDataType.Int,
            "email_address" => DigiCertCustomFieldDataType.EmailAddress,
            "email_list" => DigiCertCustomFieldDataType.EmailList,
            _ => DigiCertCustomFieldDataType.Anything
        };
    }
}