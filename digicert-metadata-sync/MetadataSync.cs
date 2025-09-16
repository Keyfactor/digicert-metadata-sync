// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Text.Json;
using DigicertMetadataSync.Client;
using DigicertMetadataSync.Logic;
using DigicertMetadataSync.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;

namespace DigicertMetadataSync;

internal class DigicertSync
{
    // create a static _logger field
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static void Main(string[] args)
    {
        // Define the config directory path
        var configDirectory = Path.Combine(Directory.GetCurrentDirectory(), "config");

        // Ensure the config directory exists
        if (!Directory.Exists(configDirectory)) Directory.CreateDirectory(configDirectory);
        // Set up NLog to load the configuration from the config folder
        var nlogConfigPath = Path.Combine(configDirectory, "nlog.config");
        if (File.Exists(nlogConfigPath))
            LogManager.Configuration = new XmlLoggingConfiguration(nlogConfigPath);
        else
            _logger.Error($"NLog configuration file not found at {nlogConfigPath}. Using default configuration.");

        // Start of the run
        var runId = Guid.NewGuid();
        _logger.Info("============================================================");
        _logger.Info($"[START] DigiCert Metadata Sync - Run at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _logger.Info($"[RUN ID: {runId}]");
        _logger.Info("============================================================");
        ///////////////////////////
        // SECTION I: Initial setup and connection testing
        _logger.Debug("Loading configuration.");
        ConfigMode configMode;
        try
        {
            if (args.Length == 0)
                throw new ArgumentException("No configuration mode provided. Please specify KFtoSC or SCtoKF.");

            // Parse the config mode from the command-line arguments
            if (!Enum.TryParse(args[0], true, out configMode))
            {
                _logger.Error("Invalid configuration mode. Please specify KFtoDC or DCtoKF.");
                throw new ArgumentException("Invalid configuration mode. Please specify KFtoDC or DCtoKF.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Unable to process tool mode: {ex.Message}");
            throw; // Use 'throw;' to preserve the original stack trace
        }

        _logger.Info($"Configuration mode set to: {configMode}");

        // Build the config
        var config = new ConfigurationBuilder().Build();
        try
        {
            config = new ConfigurationBuilder()
                .SetBasePath(configDirectory) // Set the base path to the config directory
                .AddJsonFile("config.json", false, false)
                .AddJsonFile("fields.json", false, false)
                .AddJsonFile("bannedcharacters.json", false, false)
                .Build();
        }
        catch (Exception ex)
        {
            _logger.Error($"Unable to load config file: {ex.Message}");
            throw; // Use 'throw;' to preserve the original stack trace
        }

        Config settings = new();
        List<CharDBItem> bannedCharList = new();


        try
        {
            // Required: Config
            settings = config.GetSection("Config")
                           .Get<Config>()
                       ?? throw new InvalidOperationException("Missing config section in the config json file.");

            // Optional: ManualFields & CustomFields (empty list is fine)
            _ = config.GetSection("ManualFields")
                    .Get<List<UnifiedFormatField>>(o => o.ErrorOnUnknownConfiguration = true) ??
                new List<UnifiedFormatField>();

            _ = config.GetSection("CustomFields")
                    .Get<List<UnifiedFormatField>>(o => o.ErrorOnUnknownConfiguration = true) ??
                new List<UnifiedFormatField>();

            // Optional: BannedCharacters (default to empty list)
            bannedCharList = config.GetSection("BannedCharacters")
                .Get<List<CharDBItem>>(o => o.ErrorOnUnknownConfiguration = true) ?? new List<CharDBItem>();
        }
        catch (Exception ex)
        {
            _logger.Error($"Unable to process config file: {ex.Message}");
            throw; // Use 'throw;' to preserve the original stack trace
        }

        ValueCoercion.KeyfactorDateFormat = settings.keyfactorDateFormat;
        _logger.Info("Configuration loaded successfully. Testing connection to DigiCert API and Keyfactor API.");


        // Setup the service
        var services = new ServiceCollection();
        services.AddDigiCertClient("https://www.digicert.com/services/v2/");
        services.AddKeyfactorMetadataClient(settings.keyfactorAPIUrl);
        // Build the service provider
        var provider = services.BuildServiceProvider();

        // Set up and authenticate DigiCert clients
        var dcApiKeyClient = provider.GetRequiredService<DigiCertClient>();
        dcApiKeyClient.Authenticate(settings.digicertApiKey);
        // Test connection
        var dcFields = dcApiKeyClient.ListCustomFields();

        // Test Keyfactor connection
        var kfClient = provider.GetRequiredService<KeyfactorMetadataClient>();
        // Authenticate
        kfClient.Authenticate(
            settings.keyfactorDomainAndUser,
            settings.keyfactorPassword
        );
        var kfFields = new List<KeyfactorMetadataField>();
        try
        {
            kfFields = kfClient.ListMetadataFields();
            _logger.Debug("Retrieved All Metadata Fields from Keyfactor.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to connect to Keyfactor API: {ex.Message}");
            _logger.Fatal($"Critical error: {ex.Message}");
            Environment.Exit(1); // Exit with a non-zero code to indicate failure
            throw; // Use 'throw;' to preserve the original stack trace
        }

        /////////////
        //SECTION II: Determination of field overlap
        var unifiedFieldList = new List<UnifiedFormatField>();
        try
        {
            if (settings.importAllCustomDigicertFields)
            {
                _logger.Info(
                    "importAllCustomDigicertFields is enabled. Mapping DigiCert custom fields to UnifiedFormatField. Notice: fields that have options will not have these options added" +
                    "to Keyfactor as DigiCert offers no way to retrieve options (for dropdownmenu/email list). Please consider using fields.json if you want to enable options for any metadata fields in Keyfactor.");
                unifiedFieldList = dcFields
                    // Include disabled only if explicitly enabled; treat null as active
                    .Where(f => settings.importDataForDeactivatedDigiCertFields || (f.IsActive == true))
                    .Select(f =>
                    {
                        var name = !string.IsNullOrWhiteSpace(f.Label) ? f.Label! : $"metadata_{f.Id}";
                        var dcType = f.DataType; // may be null; your mapper should handle it

                        return new UnifiedFormatField
                        {
                            // Names/descriptions (no sanitization)
                            DigicertFieldName = name,
                            KeyfactorMetadataFieldName = name,
                            KeyfactorDescription = name,
                            // Type mapping via your DigiCert -> Keyfactor mapper
                            KeyfactorDataType = Helpers.ToKeyfactorDataType(dcType),
                            // No validation/message per request
                            KeyfactorHint = string.Empty,
                            KeyfactorValidation = string.Empty,
                            KeyfactorMessage = string.Empty,
                            // Required -> enrollment required
                            KeyfactorEnrollment = f.IsRequired == true ? 1 : 0,
                            // DigiCert account metadata API doesn't return option sets
                            KeyfactorOptions = [],
                            KeyfactorAllowAPI = true,
                            // Defaults
                            KeyfactorDefaultValue = string.Empty,
                            KeyfactorDisplayOrder = 0,
                            KeyfactorCaseSensitive = false,
                            KeyfactorMetadataFieldId = 0,
                            ToolFieldType = UnifiedFieldType.Custom
                        };
                    })
                    .ToList();

                _logger.Info($"Loaded {unifiedFieldList.Count} custom fields from DigiCert.");
            }
            else
            {
                _logger.Info("importAllCustomDigicertFields is disabled. Using field mapping from configuration.");
                // This loads custom metadata using the CustomFields config.

                unifiedFieldList = config
                    .GetSection("CustomFields")
                    .Get<List<UnifiedFormatField>>(o => o.ErrorOnUnknownConfiguration = true);
                foreach (var item in unifiedFieldList) item.ToolFieldType = UnifiedFieldType.Custom;
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.Fatal($"Critical error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing custom fields: {ex.Message}");
        }

        _logger.Debug($"Loaded {unifiedFieldList.Count.ToString()} Custom Fields.");

        // Load the manual fields from the config file and add it to the field list.
        var unifiedManualFieldList = config.GetSection("ManualFields")
            .Get<List<UnifiedFormatField>>(o => o.ErrorOnUnknownConfiguration = true)?
            .Select(item =>
            {
                item.ToolFieldType = UnifiedFieldType.Manual;
                return item;
            })
            .ToList() ?? new List<UnifiedFormatField>();
        unifiedFieldList.AddRange(unifiedManualFieldList);
        _logger.Debug($"Loaded {unifiedManualFieldList.Count.ToString()} Manual Fields.");

        // Initialize a list to collect invalid character details
        var invalidCharacterDetails = new List<string>();

        // Check both lists for bad characters, ask for restart if needed.
        var restartRequired = false;

        if (settings.importAllCustomDigicertFields)
            BannedCharacters.CheckForChars(unifiedFieldList, bannedCharList, invalidCharacterDetails);
        else
            BannedCharacters.CheckForChars(unifiedFieldList, bannedCharList, invalidCharacterDetails, true);

        foreach (var badchar in bannedCharList)
            if (badchar.replacementcharacter == "null")
                restartRequired = true;

        // Serialize the banned characters list with pretty-printing
        var formattedCharList = JsonSerializer.Serialize(new { BannedCharacters = bannedCharList },
            new JsonSerializerOptions
            {
                WriteIndented = true // Enable pretty-printing
            });

        File.WriteAllText(Path.Combine(configDirectory, "bannedcharacters.json"), formattedCharList);

        // Log aggregated invalid character details if replacements are missing
        if (restartRequired && invalidCharacterDetails.Any())
        {
            _logger.Warn("The following fields contain invalid characters with no replacements:");
            foreach (var detail in invalidCharacterDetails) _logger.Warn(detail);
        }

        if (restartRequired)
        {
            // Tool needs restarting at this point. 
            var bannedChars = new Exception("Replacement characters for auto-fill for automated DigiCert custom field import need specifying. Please fill in the required data in config/bannedcharacters.json.");
            _logger.Fatal($"Critical error: {bannedChars.Message}");
            Environment.Exit(1); // Exit with a non-zero code to indicate failure
        }

        // Process the fields - run banned character replacement and send the fields off to Keyfactor.
        Parallel.ForEach(unifiedFieldList,
            field =>
            {
                field.KeyfactorMetadataFieldName =
                    BannedCharacters.ReplaceAllBannedCharacters(field.KeyfactorMetadataFieldName, bannedCharList);
            });
        kfClient.SendUnifiedMetadataFields(unifiedFieldList, kfFields);

        // Loading DigiCert metadata field IDs into the unified list (for custom fields only)
        foreach (var unifiedField in unifiedFieldList)
        {
            var matchingDcField = dcFields
                .FirstOrDefault(dc =>
                    string.Equals(dc.Label, unifiedField.DigicertFieldName,
                        StringComparison.OrdinalIgnoreCase));
            if (matchingDcField != null)
            {
                unifiedField.DigiCertMetadaFieldId = matchingDcField.Id;
                unifiedField.DigicertDataType = Helpers.ToDigiCertEnumFromString(matchingDcField.DataType);
            }

        }

        //Step 1 - pull all digicert custom fields
        // step 2 - if some if these fields do not exist in keyfactor
        //if importAllCustomDigicertFields = true put out a message stating they dont exist in digicert and need to be created manually, will be synced on next run.
        //else create them

        //If running in KFtoDC mode, we need to update the field IDs in the unified list.
        if (configMode == ConfigMode.KFtoDC)
            if (settings.createMissingFieldsInDigicert)
            {
                // STEP 1 - Identify what fields need to get pushed to digicert.
                var customFieldsOnly = unifiedFieldList.Where(f => f.ToolFieldType == UnifiedFieldType.Custom)
                    .ToList();
                var fieldsNotInDC = customFieldsOnly
                    .Where(customField => !dcFields
                        .Any(dcField => string.Equals(customField.DigicertFieldName, dcField.Label,
                            StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                // STEP 2 - Push fields to digicert
                if (fieldsNotInDC.Count > 0)
                {
                    if (settings.importAllCustomDigicertFields)
                    {
                        _logger.Info(
                            "You are operating KFtoDC with importAllCustomDigicertFields set to true in config.json and have fields that may not exist in DigiCert. Fields that do not have names exactly matched between Keyfactor and DigiCert will not" +
                            "have their contents synced. If you have fields that exist in Keyfactor but do not exist in DigiCert and you wish to sync their contents to DigiCert," +
                            "you will need to create these fields manually in DigiCert to sync them during the next run of the tool, or switch to using fields.json.");
                    }
                    else
                    {
                        _logger.Info("Creating custom fields in DigiCert to match data in fields.json.");
                        try
                        {
                            IEnumerable<DcCustomFieldCreate> fieldsToCreate = new List<DcCustomFieldCreate>();
                            foreach (var field in fieldsNotInDC)
                            {
                                var dcType = field.DigicertDataType != (int)DigiCertCustomFieldDataType.Anything
                                    ? field.DigicertDataType
                                    : Helpers.ToDigiCertDataType(field.KeyfactorDataType);
                                var (isRequired, isActive) = Helpers.ToDigiCertFlags(field.KeyfactorEnrollment);

                                var newField = new DcCustomFieldCreate
                                {
                                    Label = field.DigicertFieldName,
                                    DataType = dcType.ToWireString(),
                                    IsActive = isActive,
                                    IsRequired = isRequired // Enrollment 1 = Required
                                    // Other properties can be set as needed
                                };
                                fieldsToCreate = fieldsToCreate.Append(newField);
                            }

                            var createdField = dcApiKeyClient.BulkAddCustomFields(fieldsToCreate);
                            if (createdField.IsSuccessStatusCode)
                                _logger.Info(
                                    "Added missing fields to DigiCert.");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                $"Error adding new DigiCert custom field to DigiCert: {ex.Message}");
                        }

                        // STEP 3 - Retrieve and store the new DigiCert field IDs.
                        dcFields = dcApiKeyClient.ListCustomFields();
                        foreach (var unifiedField in unifiedFieldList)
                        {
                            var matchingDcField = dcFields
                                .FirstOrDefault(dc =>
                                    string.Equals(dc.Label, unifiedField.DigicertFieldName,
                                        StringComparison.OrdinalIgnoreCase));
                            if (matchingDcField != null) unifiedField.DigiCertMetadaFieldId = matchingDcField.Id;
                        }
                    }
                }
                else
                {
                    _logger.Info("All custom fields are already present in DigiCert. New fields will not be created.");
                }
            }
            else
            {
                _logger.Debug("Automated field creation for KFtoDC mode is disabled. Fields that exist in fields.json but do not exist in DigiCert will not be added to DigiCert.");
            }


        // Get list of all DigiCert Certs stored in Keyfactor.
        // Define pagination parameters
        var pageSize = settings.keyfactorPageSize;
        var pageNumber = 1;
        var hasMorePages = true;

        // Initialize counters and lists for tracking certificates
        var totalCertsProcessed = 0;
        var certsWithoutCustomFields = 0;

        // Initialize cumulative lists for unmatched and successfully updated certificates
        var cumulativeUnmatchedCerts = new List<string>();
        int unmatchedCount = 0;
        var cumulativePartiallyProcessedCerts = new List<string>();
        int partiallyProcessedCount = 0;
        var cumulativeSuccessfullyUpdatedCerts = new List<string>();
        int successfullyUpdatedCount = 0;

        // Initialize a list to collect certificates with missing custom fields
        var cumulativeMissingCustomFields = new List<string>();
        int missingCustomFields = 0;
        while (hasMorePages)
        {
            // Get the current page of certificates
            var certsPage = kfClient.GetCertificatesByIssuer(settings.keyfactorDigicertIssuedCertQueryTerm,
                settings.syncRevokedAndExpiredCerts, pageNumber, pageSize);
            if (certsPage.Count > 0)
            {
                _logger.Info(
                    $"[PAGE INFO] Retrieved {certsPage.Count} certificates on page {pageNumber}. Processing batch.");
                pageNumber++;
                // Process the current page of certificates
                if (configMode == ConfigMode.DCtoKF)
                    //Find matching cert by serial number 
                    foreach (var localKfCert in certsPage)
                    {
                        var dcResponse =
                            dcApiKeyClient.GetOrderBySerialOrThumbprint(localKfCert.SerialNumber,
                                localKfCert.Thumbprint);


                        if (dcResponse == null)
                        {
                            cumulativeUnmatchedCerts.Add(localKfCert.SerialNumber);
                            continue; // Skip to the next Keyfactor cert
                        }

                        // Initialize a flag to track if any fields failed to process
                        var hasPartialProcessing = false;

                        // Now we process and prep the data for Keyfactor - first load manual fields.
                        var keyfactorMetadataPayload = new Dictionary<string, object>();


                        // Process manual fields
                        foreach (var field in unifiedFieldList.Where(f => f.ToolFieldType == UnifiedFieldType.Manual))
                            try
                            {
                                var raw = Helpers.GetPropertyValue(dcResponse, field.DigicertFieldName)?.ToString();
                                if (raw != null)
                                {
                                    using var doc =
                                        JsonDocument.Parse(
                                            $"\"{raw.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""); // treat as a JSON string
                                    var coerced = ValueCoercion.Coerce(doc.RootElement,
                                        field.KeyfactorDataType, field.KeyfactorOptions);
                                    if (coerced is not null && !(coerced is string s && string.IsNullOrWhiteSpace(s)))
                                        keyfactorMetadataPayload[field.KeyfactorMetadataFieldName] = coerced;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn(
                                    $"[PAGE ERROR] Error processing manual field '{field.KeyfactorMetadataFieldName}' for cert {localKfCert.SerialNumber}: {ex.Message}");
                                hasPartialProcessing = true;
                            }

                        // Process custom fields
                        if (dcResponse.CustomFields != null && dcResponse.CustomFields.Count != 0)
                            foreach (var field in unifiedFieldList.Where(f =>
                                         f.ToolFieldType == UnifiedFieldType.Custom))
                                try
                                {
                                    // Find the DC custom field by its DigiCert label
                                    var localCustomField = dcResponse.CustomFields?
                                        .FirstOrDefault(cf => cf.Label != null &&
                                                              cf.Label.Equals(field.DigicertFieldName,
                                                                  StringComparison.OrdinalIgnoreCase));

                                    if (localCustomField != null)
                                    {
                                        var coerced = ValueCoercion.Coerce(
                                            localCustomField.Value,
                                            field.KeyfactorDataType,
                                            field.KeyfactorOptions);

                                        // Only add non-null values; this avoids overwriting existing KF values with blanks.
                                        if (coerced is not null &&
                                            !(coerced is string s && string.IsNullOrWhiteSpace(s)))
                                        {
                                            keyfactorMetadataPayload[field.KeyfactorMetadataFieldName] = coerced;
                                            _logger.Trace($"Coerced DigiCert field '{field.DigicertFieldName}' to " +
                                                          $"Keyfactor '{field.KeyfactorMetadataFieldName}' as type {field.KeyfactorDataType}: {coerced}");
                                        }
                                        else
                                        {
                                            _logger.Debug(
                                                $"Skipping empty/null value for '{field.KeyfactorMetadataFieldName}' (source '{field.DigicertFieldName}').");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warn(
                                        $"[PAGE ERROR] Error processing custom field '{field.KeyfactorMetadataFieldName}' for cert {localKfCert.SerialNumber}: {ex.Message}");
                                    hasPartialProcessing = true;
                                }
                        else
                            certsWithoutCustomFields++;

                        // Update metadata in Keyfactor
                        if (keyfactorMetadataPayload.Count == 0)
                            _logger.Debug(
                                $"Skipping metadata upload for certificate {localKfCert.SerialNumber} as the payload is empty.");
                        else
                            try
                            {
                                if (kfClient.UpdateCertificateMetadata(localKfCert.Id, keyfactorMetadataPayload))
                                {
                                    cumulativeSuccessfullyUpdatedCerts.Add(localKfCert.SerialNumber);
                                    _logger.Debug(
                                        $"Updated metadata for certificate with Thumbprint {localKfCert.Thumbprint}");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn(
                                    $"[PAGE ERROR] Error updating metadata for cert {localKfCert.SerialNumber}: {ex.Message}");
                                hasPartialProcessing = true;
                            }

                        // Update counters
                        if (hasPartialProcessing)
                            cumulativePartiallyProcessedCerts.Add(localKfCert.SerialNumber);
                        else
                            totalCertsProcessed++;
                    }
                else if (configMode == ConfigMode.KFtoDC)
                    foreach (var localKfCert in certsPage)
                        if (localKfCert.Metadata != null && localKfCert.Metadata.Count != 0)
                        {
                            bool fullyUpdatedMetadata = true;
                            try
                            {
                                // --- call site patch ---
                                var dcResponse =
                                    dcApiKeyClient.GetOrderBySerialOrThumbprint(localKfCert.SerialNumber,
                                        localKfCert.Thumbprint);
                                var dcOrderId = dcResponse.Id;

                                // Build a fast lookup for your mapping by KF metadata field name (case-insensitive)
                                var map = unifiedFieldList.Where(t => t.ToolFieldType == UnifiedFieldType.Custom)
                                    .ToDictionary(f => f.KeyfactorMetadataFieldName,
                                        f => f,
                                        StringComparer.OrdinalIgnoreCase);

                                foreach (var fieldInKf in localKfCert.Metadata)
                                {
                                    if (!map.TryGetValue(fieldInKf.Key, out var u))
                                        continue; // no mapping for this KF field

                                    var dcMetadataId =
                                        u.DigiCertMetadaFieldId; // ensure your model uses this exact name
                                    if (dcMetadataId <= 0)
                                        continue; // can't push without DigiCert metadata_id

                                    // Pick DigiCert type: config wins; else derive from KF type
                                    var dcType = u.DigicertDataType != DigiCertCustomFieldDataType.Anything
                                        ? u.DigicertDataType
                                        : Helpers.ToDigiCertDataType(u.KeyfactorDataType);

                                    var raw = fieldInKf.Value; // often string; can be object - normalize:
                                    var rawString = raw;

                                    var coerced =
                                        ValueCoercionDC.CoerceForDigiCert(rawString, dcType, u.KeyfactorOptions);

                                    if (!dcApiKeyClient.UpdateOrderCustomFieldValue(dcOrderId, dcMetadataId,
                                            coerced))
                                    {
                                        fullyUpdatedMetadata = false;
                                        _logger.Warn(
                                            $"Failed to update DigiCert custom field '{u.DigicertFieldName}' for cert {localKfCert.SerialNumber}");
                                    }
                                }

                                if (fullyUpdatedMetadata)
                                {
                                    totalCertsProcessed++; // Increment total processed count
                                    cumulativeSuccessfullyUpdatedCerts.Add(localKfCert.SerialNumber);
                                }
                                else
                                {
                                    cumulativePartiallyProcessedCerts.Add(localKfCert.SerialNumber);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(
                                    $"Error updating DigiCert custom field for cert {localKfCert.SerialNumber}: {ex.Message}");
                            }
                        }
                        else
                        {
                            certsWithoutCustomFields++;
                            cumulativeMissingCustomFields.Add(localKfCert.SerialNumber);
                        }
                else
                    throw new ArgumentException("Invalid configuration mode. Please specify KFtoSC or SCtoKF.");
                // Flushing lists to avoid memory issues on large syncs
                cumulativeSuccessfullyUpdatedCerts.FlushRemainder(
                    _logger,
                    label: "SuccessfullyUpdated",
                    totalCount: ref successfullyUpdatedCount
                );
                cumulativePartiallyProcessedCerts.FlushRemainder(
                    _logger,
                    label: "PartiallyProcessed",
                    totalCount: ref partiallyProcessedCount
                );
                cumulativeUnmatchedCerts.FlushRemainder(
                    _logger,
                    label: "UnmatchedBetweenKfAndDc",
                    totalCount: ref unmatchedCount
                );
                cumulativeMissingCustomFields.FlushRemainder(
                    _logger,
                    label: "MissingCustomFields",
                    totalCount: ref missingCustomFields
                );
            }
            else
            {
                hasMorePages = false; // No more certificates to retrieve
            }
        }

        // Log cumulative results before the application finishes
        _logger.Info(
            $"[SUMMARY] Completed retrieval and processing of certificates. Total certificates processed successfully: {totalCertsProcessed}. Certs without Custom Fields data: {certsWithoutCustomFields}.");
        if (partiallyProcessedCount + unmatchedCount > 0)
            _logger.Warn(
                $"[SUMMARY] Total certificates with partial processing or errors: {partiallyProcessedCount + unmatchedCount}.");
        if (unmatchedCount > 0)
            _logger.Warn(
                $"[SUMMARY] No matching DigiCert certificates found for {unmatchedCount} Keyfactor certs.");
        // Log aggregated warnings for missing custom fields during SCtoKF sync
        if (missingCustomFields > 0)
        {
            _logger.Info(
                $"[SUMMARY] No Metadata found for {missingCustomFields} DigiCert certificates in Keyfactor.");
        }
        // End of the run
        _logger.Info("============================================================");
        _logger.Info($"[END] DigiCert Metadata Sync - Run completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        _logger.Info($"[RUN ID: {runId}]");
        _logger.Info("============================================================");
    }
}