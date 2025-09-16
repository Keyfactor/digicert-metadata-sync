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

### ⚠️ Important Notice
**Configuration files and their location have changed since version 2.1.0** Please review the documentation and see the new stock configuration files for guidance on how to set up the tool. The configuration files will need to be placed in the `config` subdirectory for use with the tool.

This tool automates the synchronization of metadata fields between **DigiCert CertCentral** and **Keyfactor Command**. It performs two primary operations:

1. **DCtoKF** - Synchronizes *manual fields* and *custom fields* from DigiCert into Keyfactor.
2. **KFtoDC** - Synchronizes *custom fields* from Keyfactor back into DigiCert.

> **Notes**
>
> * **ManualFields** are values present in DigiCert's *Order Info* JSON and are mapped by dot path (e.g., `organization_contact.email`). Manual field data is available **only** for DigiCert -> Keyfactor sync.
> * **CustomFields** are DigiCert CertCentral custom fields and can be synchronized in **both** directions (DigiCert <--> Keyfactor).
> * The list of available **manual** fields is derived from the DigiCert *Order Info* API. See: [DigiCert Order Info API response](https://dev.digicert.com/en/certcentral-apis/services-api/orders/order-info.html)
> * Certificates must already exist in Keyfactor; this tool does **not** import certificates.

---

## Installation and Usage

### Prerequisites

- .NET **9** (or newer) runtime.
- DigiCert **API key** with **API key restrictions (optional)** set to **None** when creating the key in CertCentral.
- A Keyfactor account with API access and permission to create/edit metadata fields and modify certificates.
- The following files in the **`config`** subdirectory:
  - `config.json`
  - `fields.json`
  - `bannedcharacters.json` (auto-generated on first run if needed)

Additional notes:

- Designed for **Keyfactor 25.1**; tested compatible with older versions.
- The tool communicates directly with **Keyfactor Command API** and **DigiCert** - no Keyfactor Gateway dependency.
- Independent logging: logs are written to a local `logs/` folder next to the executable.

### Running the Tool

From the tool directory, open PowerShell and run:

```powershell
./DigicertMetadataSync.exe dctokf
```

or

```powershell
./DigicertMetadataSync.exe kftodc
```

> **Tip:** The tool performs one sync in the specified direction and then exits. Schedule it (e.g., with Windows Task Scheduler) for recurring syncs.

---

## Command Line Modes

One of the following modes must be supplied as the **first (and only) argument**:

- `dctokf`  
  Synchronizes **manual** and **custom** fields **from DigiCert to Keyfactor**.
  - Reads mappings from `fields.json` for manual fields.
  - If `importAllCustomDigicertFields` is **true**, imports *all* DigiCert custom fields; otherwise uses only those listed under `CustomFields` in `fields.json`.
  - Ensures required metadata fields exist in Keyfactor, creating missing ones.
  - Locates DigiCert-issued certs in Keyfactor (by Issuer DN filter).
  - Updates Keyfactor metadata with coerced values.

- `kftodc`  
  Synchronizes **custom** fields **from Keyfactor to DigiCert**.
  - Reads mappings from `fields.json` for custom fields.
  - Ensures required metadata fields exist in Keyfactor.
  - If `createMissingFieldsInDigicert` is **true** and `importAllCustomDigicertFields` is **false**, attempts to create missing DigiCert custom fields (limited by DigiCert API capabilities).
  - Locates DigiCert-issued certs in Keyfactor (by Issuer DN filter).
  - Updates DigiCert custom field values with coerced data types.

> **Important:** Run `dctokf` at least once before running `kftodc` so Keyfactor metadata fields exist and have been normalized.

---

## Settings

### 1. `config\config.json`

> See `stock-config.json` for a complete example.

- **`digicertApiKey`** - CertCentral API key. Use a key created with **API key restrictions = None**.
- **`keyfactorDomainAndUser`** - e.g., `DOMAIN\\Username`. User must be permitted to use the Keyfactor API, create/edit metadata fields, and edit certificates.
- **`keyfactorPassword`** - Password for the Keyfactor user.
- **`keyfactorApiUrl`** - Root Keyfactor API URL, e.g., `https://your-keyfactor-server/keyfactorapi/`.
- **`keyfactorDigicertIssuedCertQueryTerm`** - Substring matched against Issuer DN to identify DigiCert‑issued certificates (e.g., `"DigiCert"`).
- **`importAllCustomDigicertFields`** - If `true`, import all DigiCert custom fields and auto-create Keyfactor metadata fields to match (ignores `CustomFields` entries).
- **`importDataForDeactivatedDigiCertFields`** - If `true`, process DigiCert fields even if deactivated.
- **`syncRevokedAndExpiredCerts`** - If `true`, include revoked and expired certificates in sync.
- **`keyfactorPageSize`** - Batch size for Keyfactor certificate processing (default: `100`).
- **`keyfactorDateFormat`** - Date format for Keyfactor writes (defaults vary by Keyfactor version; `M/d/yyyy h:mm:ss tt` for 25.1, `yyyy-MM-dd` for some older Keyfactor versions).
- **`createMissingFieldsInDigicert`** - If `true` (and `importAllCustomDigicertFields` is `false`), create missing DigiCert custom fields when syncing KF→DC (subject to DigiCert API limitations).

---

### 2. `config\fields.json`

> See `stock-fields.json` for examples.

For each mapping:

- **`digicertFieldName`** - DigiCert field name; for manual fields, a **dot path** into the Order Info JSON.
- **`digicertCustomFieldDataType`** - Input type for DigiCert **custom** fields:  
  `0` = Anything, `1` = Text, `2` = Int, `3` = EmailAddress, `4` = EmailList.  
  *(Dropdowns are not supported by the DigiCert API.)*
- **`keyfactorMetadataFieldName`** - Target Keyfactor metadata field name (**[A-Za-z0-9-_]** only; no spaces).
- **`keyfactorDescription`** - Description shown in Keyfactor.
- **`keyfactorDataType`** - Keyfactor type: `1` String, `2` Integer, `3` Date, `4` Boolean, `5` MultipleChoice, `6` BigText, `7` Email.
- **`keyfactorHint`** - UI hint text in Keyfactor.
- **`keyfactorValidation`** - Regex validation (string fields only).
- **`keyfactorEnrollment`** - Enrollment behavior (e.g., `0` Optional, `1` Required, `2` Hidden).
- **`keyfactorMessage`** - Validation failure message.
- **`keyfactorOptions`** - Values for MultipleChoice (ignored otherwise).
- **`keyfactorDefaultValue`** - Default value, if applicable.
- **`keyfactorDisplayOrder`** - Display order in Keyfactor.
- **`keyfactorCaseSensitive`** - Whether validation is case-sensitive (string fields with validation).
 
Please review this for the exact values available for each `keyfactor` field: [Keyfactor API Reference](https://software.keyfactor.com/Core-OnPrem/v25.2/Content/WebAPI/KeyfactorAPI/MetadataFieldsPost.htm)

---

### 3. `config\bannedcharacters.json`

Generated on first `dctokf` run if DigiCert custom field names contain characters not permitted by Keyfactor (only alphanumeric, `-`, and `_` are allowed). Fill in `replacementCharacter` for each banned `character`, then re-run.

**Example:**

```jsonc
[
  { "character": " ", "replacementCharacter": "_" },
  { "character": "/", "replacementCharacter": "-" }
]
```

If any `replacementCharacter` remains `null`, the tool exits with an error on the next run.

---

### 4. `config\nlog.config` (Logging)

Logging uses **NLog** and writes to a local `logs/` folder.

- Configure minimum levels and targets in `rules`.
- Two files are typically produced: a main log (all levels) and an error-only log.

> Adjust `minLevel` in the `<rules>` section to change verbosity. Available levels: `Trace`, `Debug`, `Info`. `Info` for default.

---

## Example Workflow

1. **Initial Setup**
   - Populate `config\config.json` with DigiCert and Keyfactor credentials and settings.
   - Define `ManualFields` and `CustomFields` lists in `config\fields.json`.

2. **First Run (Detect Banned Characters)**
   ```powershell
   ./DigicertMetadataSync.exe dctokf
   ```
   - If banned characters are found in DigiCert custom field names, the tool logs a warning and exits.
   - A `bannedcharacters.json` file is created with `replacementCharacter: null` entries.

3. **Populate Replacements**
   - Edit `config\bannedcharacters.json` and set `replacementCharacter` values.
   - Save the file.

4. **Second Run (Create Fields & Sync Data)**
   ```powershell
   ./DigicertMetadataSync.exe dctokf
   ```
   - Fields are created/validated; data is synchronized DigiCert -> Keyfactor.
   - (Optional) Run `kftodc` to push Keyfactor values back to DigiCert custom fields.

---

## How It Works

### DigiCert -> Keyfactor (`dctokf`)
1. Read manual mappings from `fields.json`.
2. Read custom fields from DigiCert (all if `importAllCustomDigicertFields` is `true`; otherwise only those listed).
3. Ensure Keyfactor metadata fields exist (create missing).
4. Query Keyfactor for DigiCert-issued certs (Issuer DN filter).
5. For each certificate:
   - Fetch DigiCert order data (manual + custom).
   - Coerce types to Keyfactor formats.
   - Update Keyfactor metadata values.

### Keyfactor -> DigiCert (`kftodc`)
1. Read custom field mappings from `fields.json`.
2. Ensure Keyfactor metadata fields exist (create missing).
3. If enabled, create missing DigiCert custom fields (API limitations apply).
4. Query Keyfactor for DigiCert-issued certs.
5. For each certificate:
   - Read Keyfactor metadata values.
   - Coerce to DigiCert data types.
   - Update DigiCert custom field values on the order.

**Retry logic:** When DigiCert rate-limits, the tool honors the DigiCert-supplied backoff time before retrying.

---

## Usage Recommendations

- Schedule periodic runs using Windows Task Scheduler (or equivalent).
- Run from the tool's directory; ensure the account can read/write the `config` folder.
- Sync is **destructive** for the destination side (values are overwritten in the destination of the chosen direction).
- Differential change tracking is **not** supported due to DigiCert and Keyfactor API limitations.

---

## Troubleshooting

- **Authentication errors** - Verify DigiCert API key and Keyfactor credentials/URL.
- **Keyfactor field name errors** - Ensure `bannedcharacters.json` replacements are set and valid.
- **Field creation failures** - Check Keyfactor logs for details; API errors may be non-specific.
- **Custom fields with options in DigiCert** - The DigiCert API cannot create dropdown/option fields; create these manually in CertCentral.

---



## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor integrations](https://github.com/topics/keyfactor-integration).