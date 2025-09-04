<h1 align="center" style="border-bottom: none">
    DigiCert Metadata Sync
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/digicert-metadata-sync/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/digicert-metadata-sync?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/digicert-metadata-sync?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/digicert-metadata-sync/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a> 
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/topics/keyfactor-integration">
    <b>Related Integrations</b>
  </a>
</p>

## Support
The DigiCert Metadata Sync is open source and there is **no SLA**. Keyfactor will address issues as resources become available. Keyfactor customers may request escalation by opening up a support ticket through their Keyfactor representative. 

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.


## Overview
This tool can:

1. **Create/align metadata fields in Keyfactor** to match:
   - DigiCert **Custom Metadata Fields** (aka “custom fields”), and
   - Useful **non‑custom fields** from DigiCert order/certificate data (e.g., DigiCert Order ID, organization contact details). These are called **manual fields** in this project.
2. **Sync field values** from DigiCert back into the matching Keyfactor metadata fields.

Before running, configure **`DigicertMetadataSync.dll.config`** and **`manualfields.json`** (both described below).

---

## Settings

### Command Line Arguments
One of these two arguments needs to be used for the tool to run.

- **"kftodc"**
Syncronizes the contents of custom fields listed in manualfields.json from Keyfactor to DigiCert. If the fields in manualfields.json do not exist in Keyfactor or DigiCert, they are created first. Example: `.\DigicertMetadataSync.exe kftodc`

- **"dctokf"**
Syncronizes the contents of both custom and non-custom fields from DigiCert to Keyfactor. The fields are listed in manualfields.json, and are created if necessary. Example: `.\DigicertMetadataSync.exe dctokf`

### `DigicertMetadataSync.dll.config` keys
- **`DigicertAPIKey`**  
  Your DigiCert API Key with **API key restrictions** set to **Orders, Domains and Organizations".

- **`DigicertAPIKeyTopPerm`**  
  A second DigiCert API key with **top-level permissions** (needed to read all custom fields).In DigiCert, to obtain one, you must keep the API key’s **API key restrictions** at **None** when creating it.

- **`KeyfactorDomainAndUser`**  
  A Keyfactor login in the format `DOMAIN\username` with permissions to create metadata fields and update certificates.

- **`KeyfactorPassword`**  
  Password for the `KeyfactorDomainAndUser` account.

- **`KeyfactorCertSearchReturnLimit`**  
  Maximum number of certificates the tool will request from Keyfactor when searching (use a number that exceeds the total number of certs you are trying sync when running in prod).

- **`KeyfactorAPIEndpoint`**  
  Your Keyfactor API base URL, e.g. `https://example.com/KeyfactorAPI/`

- **`KeyfactorDigicertIssuedCertQueryTerm`**  
  A substring present in the **Issuer DN** of DigiCert‑issued certs in your Keyfactor instance (e.g., `"DigiCert"`). Used to scope the Keyfactor query to DigiCert certs only.

- **`ImportAllCustomDigicertFields`** (boolean)  
  If `true`, import **all** custom fields from DigiCert automatically (labels are auto‑converted to valid Keyfactor names). If `false`, only the custom fields listed in the **`CustomFields`** section of `manualfields.json` are imported.

- **`ReplaceDigicertWhiteSpaceCharacterInName`**  
  When `ImportAllCustomDigicertFields=true`, DigiCert labels that contain spaces will be converted to Keyfactor‑safe names using this string. Example: set to `"_"` to turn `"Requester Email"` into `"Requester_Email"`.

- **`SyncReissue`** (boolean)
  When `true`, the Keyfactor lookup includes **revoked** and **expired** certificates (`pq.includeRevoked=true&pq.includeExpired=true`).

### Example `DigicertMetadataSync.dll.config`
File is available within the repository named as App.config (**should be renamed to DigicertMetadataSync.dll.config for actual use**).
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
	<appSettings>
		<add key="DigicertAPIKey" value="" />
		<add key="DigicertAPIKeyTopPerm" value="" />
		<add key="KeyfactorDomainAndUser" value="" />
		<add key="KeyfactorPassword" value="" />
		<add key="KeyfactorCertSearchReturnLimit" value="5000000" />
		<add key="KeyfactorAPIEndpoint" value="" />
		<add key="KeyfactorDigicertIssuedCertQueryTerm" value="DigiCert" />
		<add key="ImportAllCustomDigicertFields" value="False" />
		<add key="ReplaceDigicertWhiteSpaceCharacterInName" value="_-_" />
		<add key="ImportDataForDeactivatedDigiCertFields" value="False" />
		<add key="SyncReissue" value="False" />
	</appSettings>
</configuration>
```
---

## `manualfields.json`

This file tells the tool **which fields** to create in Keyfactor and **how to map** DigiCert values into them. It has two top‑level arrays:

```jsonc
{
  "ManualFields": [ /* non-custom DigiCert fields you want to map */ ],
  "CustomFields": [ /* DigiCert Custom Metadata Fields you want to map */ ]
}
```

### Common properties (apply to entries in **both** arrays)

| Property | Type | Required | Description |
|---|---|---:|---|
| `DigicertFieldName` | string | yes | For **ManualFields**: a **dot path** into the DigiCert order/cert object (e.g., `organization_contact.email`). For **CustomFields**: the **label** of the DigiCert custom field _exactly as it appears_ in DigiCert. |
| `KeyfactorMetadataFieldName` | string | no | The Keyfactor field name to create/use. If omitted for **CustomFields**, the tool will derive a safe name from `DigicertFieldName` (label), replacing spaces with `ReplaceDigicertWhiteSpaceCharacterInName`. |
| `KeyfactorDescription` | string | no | Description to show in Keyfactor for this field. |
| `KeyfactorDataType` | string | yes | One of the **Keyfactor data types** listed below (e.g., `"String"`, `"Integer"`…). |
| `KeyfactorHint` | string | no | Hint to display in Keyfactor’s UI (e.g., sample format). |
| `KeyfactorAllowAPI` | bool/string | no | Defaults to `true`. Leave `true` to allow this tool to manage the field via API. |

#### New/advanced properties
These map to Keyfactor’s Metadata Field schema and are optional unless your use case requires them.

| Property | Type | Required | Notes |
|---|---|---:|---|
| `KeyfactorValidation` | string | no | Regex for **String** fields only. If set, `KeyfactorMessage` should describe the validation failure. |
| `KeyfactorMessage` | string | no | Validation error message shown if regex fails. |
| `KeyfactorEnrollment` | string | no | One of: `"Optional"` (default), `"Required"`, `"Hidden"`. |
| `KeyfactorOptions` | array\<string> | no | Choices for **Multiple Choice** fields. You may list them as an **array** in config; the tool serializes them to a **comma‑separated string** when posting to Keyfactor. |
| `KeyfactorDefaultValue` | string | no | Default field value. |

### Example `manualfields.json`
File is available within the repository.
```json
{
  "ManualFields": [
    {
      "DigicertFieldName": "id",
      "KeyfactorMetadataFieldName": "DigicertID",
      "KeyfactorDescription": "Digicert Assigned Cert ID",
      "KeyfactorDataType": "Integer",
      "KeyfactorHint": "",
      "KeyfactorAllowAPI": "True",
      "KeyfactorValidation": "",
      "KeyfactorMessage": "",
      "KeyfactorEnrollment": "Optional",
      "KeyfactorOptions": [],
      "KeyfactorDefaultValue": ""
    }
  ],
  "CustomFields": [
    {
      "DigicertFieldName": "Field2",
      "KeyfactorMetadataFieldName": "Field2",
      "KeyfactorDescription": "Test",
      "KeyfactorDataType": "String",
      "KeyfactorHint": "Pick one or more",
      "KeyfactorAllowAPI": "True",
      "KeyfactorValidation": "",
      "KeyfactorMessage": "",
      "KeyfactorEnrollment": "Optional",
      "KeyfactorOptions": "",
      "KeyfactorDefaultValue": "Test"
    }
  ]
}
```

> **Notes**  
> • For **ManualFields**, `DigicertFieldName` must match the **flattened path** into the DigiCert order object returned by the Order Info API (e.g., `organization_contact.email`).  
> • For **CustomFields**, `DigicertFieldName` must match the **label** in DigiCert (the tool will convert it to a Keyfactor‑safe name if you omit `KeyfactorMetadataFieldName`).  
> • If `ImportAllCustomDigicertFields=true`, you can leave `CustomFields` empty—every custom field in DigiCert will be created/mapped automatically.

---

## Keyfactor Metadata Field Data Types

Use the **names** below in `KeyfactorDataType` (the tool maps them to the corresponding numeric codes internally when calling the Keyfactor API):

| Name | Code | Typical uses |
|---|---:|---|
| `String` | 1 | Free text / short text |
| `Integer` | 2 | Numeric values |
| `Date` | 3 | Dates |
| `Boolean` | 4 | True/False |
| `Multiple Choice` | 5 | Fixed list of values (uses `KeyfactorOptions`) |
| `Big Text` | 6 | Long multi‑line text |

---

## Sync flow (high level)
1. **Field alignment:** Creates/updates metadata fields in Keyfactor as defined in `manualfields.json` (plus, optionally, all DigiCert custom fields).  
1a. If a field is detected as having invalid characters that Keyfactor does not accept, the tool exits with an appropriate message and replacechar.json is populated. 
2. **Value sync:** Depending on whether you set the tool to `kftodc` or `dctokf`, the values are synced either from Keyfactor to DigiCert or from DigiCert to Keyfactor.  


---

## Troubleshooting tips
- Validation only applies to **String** fields—omit `KeyfactorValidation`/`KeyfactorMessage` for other types.



## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor integrations](https://github.com/topics/keyfactor-integration).