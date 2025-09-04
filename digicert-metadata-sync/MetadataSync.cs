// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using Polly;
using RestSharp;
using RestSharp.Authenticators;
using ConfigurationManager = System.Configuration.ConfigurationManager;
namespace DigicertMetadataSync;

internal partial class DigicertSync
{
    // create a static _logger field
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static void Main(string[] args)
    {
        _logger.Debug("Start sync");
        var digicertapikey = ConfigurationManager.AppSettings.Get("DigicertAPIKey");
        var digicertapikeytopperm = ConfigurationManager.AppSettings.Get("DigicertAPIKeyTopPerm");
        var keyfactorusername = ConfigurationManager.AppSettings.Get("KeyfactorDomainAndUser");
        var keyfactorpassword = ConfigurationManager.AppSettings.Get("KeyfactorPassword");
        var importdeactivated =
            Convert.ToBoolean(ConfigurationManager.AppSettings.Get("ImportDataForDeactivatedDigiCertFields"));
        var batchsize = 200;
        var importallcustomdigicertfields =
            Convert.ToBoolean(ConfigurationManager.AppSettings.Get("ImportAllCustomDigicertFields"));
        _logger.Debug("Settings: importallcustomdigicertfields={0}",
            importallcustomdigicertfields);
        var config_mode = args[0];
        if (CheckMode(config_mode) == false)
        {
            _logger.Error("Inappropriate configuration mode. Check your command line arguments.");
            throw new Exception("Inappropriate configuration mode. Check your command line arguments.");
        }

        var digicertIssuerQueryterm = ConfigurationManager.AppSettings.Get("KeyfactorDigicertIssuedCertQueryTerm");
        var returnlimit = ConfigurationManager.AppSettings.Get("KeyfactorCertSearchReturnLimit");
        var keyfactorapilocation = ConfigurationManager.AppSettings.Get("KeyfactorAPIEndpoint");
        var syncreissue = Convert.ToBoolean(ConfigurationManager.AppSettings.Get("SyncReissue"));
        if (syncreissue)
            Console.WriteLine("Reissued and revoked cert data will be synced");
        else
            Console.WriteLine("Reissued and revoked cert data will not be synced");
        var returnlimitint = int.Parse(returnlimit);
        var numberOfBatches = (int)Math.Ceiling((double)returnlimitint / batchsize);
        _logger.Debug("Loaded config. Starting metadata field name processing.");


        // Initializing Keyfactor net client
        var kfoptions = new RestClientOptions();
        kfoptions.Authenticator = new HttpBasicAuthenticator(keyfactorusername, keyfactorpassword);
        var kfclient = new RestClient(kfoptions);

        //Initializing DigiCert net client
        var digicertClient = new RestClient();

        //Getting list of custom metadata fields from Keyfactor
        var getmetadalistkf = keyfactorapilocation + "MetadataFields";
        var metadatakfrequest = new RestRequest(getmetadalistkf);
        metadatakfrequest.AddHeader("Accept", "application/json");
        metadatakfrequest.AddHeader("x-keyfactor-api-version", "1");
        metadatakfrequest.AddHeader("x-keyfactor-requested-with", "APIClient");
        var metadatakfresponse = new RestResponse();
        GlobalRetryPolicy.RetryPolicy.Execute(() =>
        {
            try
            {
                metadatakfresponse = kfclient.Execute(metadatakfrequest);
                ;
                if (!metadatakfresponse.IsSuccessful)
                {
                    var msg = "Something went wrong while retrieving list of custom metadata fields from Keyfactor.";
                    _logger.Error(msg);
                    throw new CustomException(msg, new Exception("Request failed."));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected error: {ex}");
                throw;
            }

            _logger.Debug("Got list of custom fields from Keyfactor.");
        });
        //var metadatakfrawresponse = metadatakfresponse.Content;
        //var kfmetadatafields = JsonConvert.DeserializeObject<List<KeyfactorMetadataInstance>>(metadatakfrawresponse);
        Console.WriteLine("Got list of custom fields from Keyfactor.");

        //Getting list of custom metadata fields on DigiCert
        var customdigicertmetadatafieldlist =
            GrabCustomFieldsFromDigiCert(digicertapikey, importdeactivated, digicertClient);

        //Convert DigiCert custom fields to Keyfactor appropriate ones
        //This depends on whether the setting to import all fields was enabled or not

        var config = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("manualfields.json").Build();
        var kfcustomfields = new List<ReadInMetadataField>();

        if (importallcustomdigicertfields)
        {
            _logger.Debug("Loading custom fields using autofill");
            //This imports all the custom fields based on the list of metadata from DigiCert and does autofill
            for (var i = 0; i < customdigicertmetadatafieldlist.Count; i++)
            {
                var localkffieldinstance = new ReadInMetadataField();
                var kfdatatype = "String";
                if (customdigicertmetadatafieldlist[i].data_type != null)
                    localkffieldinstance.KeyfactorDataType = customdigicertmetadatafieldlist[i].data_type;
                else
                    localkffieldinstance.KeyfactorDataType = "String";
                if (customdigicertmetadatafieldlist[i].label != null)
                {
                    /*
                        NOTICE: KEYFACTOR DOES NOT SUPPORT SPACES IN METADATA FIELD NAMES.
                    WHITESPACE MUST BE REMOVED FROM THE NAME.
                    CURRENTLY REPLACING WITH "_-_" AS STAND IN FOR SPACE CHARACTER.
                        */
                    localkffieldinstance.DigicertFieldName = customdigicertmetadatafieldlist[i].label;
                    localkffieldinstance.KeyfactorMetadataFieldName = customdigicertmetadatafieldlist[i].label;
                    _logger.Debug("DC field name {0} becomes {1} in Keyfactor", localkffieldinstance.DigicertFieldName,
                        localkffieldinstance.KeyfactorMetadataFieldName);
                }
                else
                {
                    localkffieldinstance.DigicertFieldName = "";
                    localkffieldinstance.KeyfactorMetadataFieldName = "";
                }

                if (customdigicertmetadatafieldlist[i].description != null)
                    localkffieldinstance.KeyfactorDescription = customdigicertmetadatafieldlist[i].description;
                else
                    localkffieldinstance.KeyfactorDescription = "None.";

                localkffieldinstance.KeyfactorAllowAPI = "True";
                localkffieldinstance.KeyfactorHint = "";
                //Other parameters like enrollment can be set here too.

                kfcustomfields.Add(localkffieldinstance);
            }
        }
        else
        {
            // This loads custom metadata using the manualfields config.
            // Converts blank fields etc and preps the data.
            var customfieldslst = "CustomFields";
            kfcustomfields = config.GetSection(customfieldslst).Get<List<ReadInMetadataField>>();
            if (kfcustomfields == null) kfcustomfields = new List<ReadInMetadataField>();
            _logger.Debug("Loading custom fields using json, no autofill/conversion");
        }

        foreach (var item in kfcustomfields) item.FieldType = "Custom";


        //Adding metadata fields for the ID and the email of the requester from DigiCert.
        var kfmanualfields = new List<ReadInMetadataField>();
        var manualfieldslist = "ManualFields";
        kfmanualfields = config.GetSection(manualfieldslist).Get<List<ReadInMetadataField>>();
        if (kfmanualfields == null) kfmanualfields = new List<ReadInMetadataField>();
        foreach (var item in kfmanualfields) item.FieldType = "Manual";
        _logger.Debug("Performed field conversion.");

        //Pulling list of existing metadata fields from Keyfactor for later comparison.
        var noexistingfields = true;

        var existingmetadataurl = keyfactorapilocation + "MetadataFields";
        var existingmetadatareq = new RestRequest(existingmetadataurl);
        existingmetadatareq.AddHeader("Accept", "application/json");
        existingmetadatareq.AddHeader("x-keyfactor-api-version", "1");
        existingmetadatareq.AddHeader("x-keyfactor-requested-with", "APIClient");
        var existingmetadataresponse = new RestResponse();
        GlobalRetryPolicy.RetryPolicy.Execute(() =>
        {
            try
            {
                existingmetadataresponse = kfclient.Execute(existingmetadatareq);
                if (!existingmetadataresponse.IsSuccessful)
                {
                    var msg = "Failed to retrieve list of existing metadata fields from Keyfactor.";
                    _logger.Error(msg);
                    throw new CustomException(msg, new Exception("Request failed."));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected error: {ex}");
                throw;
            }

            _logger.Debug("Pulled existing metadata fields from Keyfactor.");
        });
        var existingmetadatalist = new List<KeyfactorMetadataInstance>();
        if (existingmetadataresponse != null)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore
                // Converter already attached via attribute; you could also add it here if preferred:
                // Converters = { new StringOrArrayToListConverter() }
            };

            existingmetadatalist =
                JsonConvert.DeserializeObject<List<KeyfactorMetadataInstance>>(
                    existingmetadataresponse.Content, settings);

            noexistingfields = false;
        }

        Console.WriteLine("Pulled existing metadata fields from keyfactor.");


        // Carrying out the persistent banned character database check

        // Loading up the character database
        var currentDirectory = Directory.GetCurrentDirectory();

        var filePath = Path.Combine(currentDirectory, "replacechar.json");

        var restartandconfigrequired = false;

        var allBannedChars = JsonConvert.DeserializeObject<List<CharDBItem>>(File.ReadAllText(filePath));

        if (importallcustomdigicertfields)
        {
            CheckForChars(kfmanualfields, allBannedChars, restartandconfigrequired);
            CheckForChars(kfcustomfields, allBannedChars, restartandconfigrequired);

            var formattedjsonchars = JsonConvert.SerializeObject(allBannedChars, Formatting.Indented);
            File.WriteAllText(filePath, formattedjsonchars);

            foreach (var badchar in allBannedChars)
                if (badchar.replacementcharacter == "null")
                {
                    restartandconfigrequired = true;
                    break;
                }

            if (restartandconfigrequired)
            {
                _logger.Error(
                    "Please replace \"null\" with your desired replacement characters in replacechar.json and re-run the tool! Only alphanumerics, \"-\" and \"_\" are allowed");
                Console.WriteLine(
                    "Please replace \"null\" with your desired replacement characters in replacechar.json and re-run the tool! Only alphanumerics, \"-\" and \"_\" are allowed");
                Environment.Exit(0);
            }
        }

        // Converting the read in fields into sendable lists
        var convertedmanualfields = convertlisttokf(kfmanualfields, allBannedChars, importallcustomdigicertfields);
        var convertedcustomfields = convertlisttokf(kfcustomfields, allBannedChars, importallcustomdigicertfields);


        _logger.Trace("Sending following manual fields to KF: {0}", JsonConvert.SerializeObject(convertedmanualfields));
        var totalfieldsadded = 0;

        //If all the fields are absent from Keyfactor, the fields are added.
        var manualresult = AddFieldsToKeyfactor(convertedmanualfields, existingmetadatalist, noexistingfields,
            keyfactorusername, keyfactorpassword, keyfactorapilocation, kfclient);
        _logger.Trace("Sending following custom fields to KF: {0}", JsonConvert.SerializeObject(convertedcustomfields));

        var customresult = AddFieldsToKeyfactor(convertedcustomfields, existingmetadatalist, noexistingfields,
            keyfactorusername, keyfactorpassword, keyfactorapilocation, kfclient);

        totalfieldsadded += manualresult.Item1;
        totalfieldsadded += customresult.Item1;

        var allnewfields = manualresult.Item2.Concat(customresult.Item2).ToList();


        //Processing this batch
        Console.WriteLine($"Added custom fields to Keyfactor. Total fields added: {totalfieldsadded.ToString()}");
        _logger.Debug($"Added custom fields to Keyfactor. Total fields added: {totalfieldsadded.ToString()}");

        // Syncing Data from Keyfactor TO DigiCert
        // Sync from DigiCert to Keyfactor must run at least once prior to this - only runs with custom fields
        if (config_mode == "kftodc")
        {
            // Initialize variable to keep track of items downloaded so far
            var certsdownloaded = 0;
            var certcounttracker = 0;
            var totalcertsprocessed = 0;
            var numcertsdatauploaded = 0;
            for (var batchnum = 0; batchnum < numberOfBatches; batchnum++)
            {
                // Check if reaching the arbitrary limit
                if (certsdownloaded + batchsize > returnlimitint)
                {
                    Console.WriteLine($"Stopped downloading at the configured limit of {returnlimitint} items.");
                    _logger.Debug($"Stopped downloading at the configured limit of {returnlimitint} items.");
                    break;
                }


                var fullcustomdgfieldlist = new List<DigicertCustomFieldInstance>();
                var newcustomfieldsfordg = new List<DigicertCustomFieldInstance>();

                // Download the items in this batch 
                var digicertlookup = keyfactorapilocation + "Certificates?pq.queryString=IssuerDN%20-contains%20%22"
                                                          + digicertIssuerQueryterm + "%22&pq.returnLimit=" +
                                                          batchsize +
                                                          "&includeMetadata=true" + "&pq.pageReturned=" + batchnum;
                if (syncreissue) digicertlookup += "&pq.includeRevoked=true&pq.includeExpired=true";
                var request = new RestRequest(digicertlookup);
                request.AddHeader("Accept", "application/json");
                request.AddHeader("x-keyfactor-api-version", "1");
                request.AddHeader("x-keyfactor-requested-with", "APIClient");
                var kflookupresponse = new RestResponse();
                GlobalRetryPolicy.RetryPolicy.Execute(() =>
                {
                    try
                    {
                        kflookupresponse = kfclient.Execute(request);
                        if (!kflookupresponse.IsSuccessful)
                        {
                            var msg =
                                "Something went wrong while retrieving batch of DigiCert issued certs from Keyfactor.";
                            _logger.Error(msg);
                            throw new CustomException(msg, new Exception("Request failed."));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Unexpected error: {ex}");
                        throw;
                    }

                    _logger.Debug("Got DigiCert issued certs from keyfactor");
                });
                var rawresponse = kflookupresponse.Content;
                var certlist = JsonConvert.DeserializeObject<List<KeyfactorCert>>(rawresponse,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Console.WriteLine("Got DigiCert issued certs from keyfactor");

                // Rebuild the list of metadata field names as they are on DigiCerts side. 

                // This covers all of the custom fields on Digicerts side
                foreach (var dgcustomfield in customdigicertmetadatafieldlist)
                {
                    var localdigicertfieldinstance = new DigicertCustomFieldInstance();

                    localdigicertfieldinstance.label = dgcustomfield.label;
                    localdigicertfieldinstance.is_active = dgcustomfield.is_active;
                    localdigicertfieldinstance.data_type = dgcustomfield.data_type;
                    localdigicertfieldinstance.is_required = dgcustomfield.is_required;

                    foreach (var kffieldeq in kfcustomfields)
                        if (dgcustomfield.label == kffieldeq.DigicertFieldName)
                            localdigicertfieldinstance.kf_field_name = kffieldeq.DigicertFieldName;

                    fullcustomdgfieldlist.Add(localdigicertfieldinstance);
                }


                //This covers all of the new fields on Keyfactors side, including new ones - needs to have digicert ids for the new ones
                foreach (var kfcustomfield in kfcustomfields)
                {
                    var localdigicertfieldinstance = new DigicertCustomFieldInstance();
                    localdigicertfieldinstance.label = kfcustomfield.DigicertFieldName;
                    localdigicertfieldinstance.is_active = true;
                    localdigicertfieldinstance.kf_field_name = kfcustomfield.KeyfactorMetadataFieldName;
                    if (kfcustomfield.KeyfactorDataType == "String")
                        localdigicertfieldinstance.data_type = "text";
                    else if (kfcustomfield.KeyfactorDataType == "Int")
                        localdigicertfieldinstance.data_type = "int";
                    else
                        localdigicertfieldinstance.data_type = "anything";
                    localdigicertfieldinstance.is_required = false;

                    if (!fullcustomdgfieldlist.Any(p => p.label == localdigicertfieldinstance.label))
                    {
                        fullcustomdgfieldlist.Add(localdigicertfieldinstance);
                        newcustomfieldsfordg.Add(localdigicertfieldinstance);
                    }
                }

                //Add fields that don't exist on DigiCert to Digicert 
                _logger.Trace("Adding following fields to DigiCert: {0}",
                    JsonConvert.SerializeObject(newcustomfieldsfordg));
                foreach (var newdgfield in newcustomfieldsfordg)
                {
                    var digicertapilocation = "https://www.digicert.com/services/v2/account/metadata";
                    var digicertnewfieldsclient = new RestClient();
                    var digicertnewfieldsrequest = new RestRequest(digicertapilocation);
                    digicertnewfieldsrequest.AddHeader("Accept", "application/json");
                    digicertnewfieldsrequest.AddHeader("X-DC-DEVKEY", digicertapikeytopperm);
                    var serializedsyncfield = JsonConvert.SerializeObject(newdgfield);
                    digicertnewfieldsrequest.AddParameter("application/json", serializedsyncfield,
                        ParameterType.RequestBody);
                    var digicertresponsenewfields = digicertnewfieldsclient.Post(digicertnewfieldsrequest);
                }


                // Grabbing the list again from digicert, populating ids for new ones 
                //Getting list of custom metadata fields on DigiCert
                var updatedmetadatafieldlist =
                    GrabCustomFieldsFromDigiCert(digicertapikey, importdeactivated, digicertClient);
                foreach (var subitem in updatedmetadatafieldlist)
                foreach (var fulllistitem in fullcustomdgfieldlist)
                    if (subitem.label == fulllistitem.label)
                        fulllistitem.id = subitem.id;


                // Pushing the data to DigiCert
                var certlist2 = JsonConvert.DeserializeObject<dynamic>(rawresponse,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                foreach (var cert in certlist2)
                {
                    Dictionary<string, string> kfstoredmetadata =
                        cert["Metadata"].ToObject<Dictionary<string, string>>();

                    var certhascustomfields = false;
                    foreach (var checkfield in fullcustomdgfieldlist)
                        if (kfstoredmetadata.ContainsKey(checkfield.kf_field_name))
                            certhascustomfields = true;

                    if (certhascustomfields)
                    {
                        var kfserialnumber = cert["SerialNumber"].ToString();

                        var digicertnewlookupurl = "https://www.digicert.com/services/v2/order/certificate" +
                                                   "?filters[serial_number]=" + kfserialnumber;

                        var newbodytemplate = new RootDigicertLookup();
                        var newsearchcriterioninstance = new SearchCriterion();
                        newbodytemplate.searchCriteriaList.Add(newsearchcriterioninstance);
                        var lookupnewrequest = new RestRequest(digicertnewlookupurl);
                        lookupnewrequest.AddHeader("Content-Type", "application/json");
                        lookupnewrequest.AddHeader("X-DC-DEVKEY", digicertapikey);

                        var digicertnewlookupresponse = new RestResponse();


                        GlobalRetryPolicy.RetryPolicy.Execute(() =>
                        {
                            try
                            {
                                digicertnewlookupresponse = digicertClient.Execute(lookupnewrequest);
                                if (!digicertnewlookupresponse.IsSuccessful)
                                {
                                    string msg = "Something went wrong while retrieving data for cert with serial" +
                                                 kfserialnumber + " from DigiCert.";
                                    _logger.Error(msg);
                                    throw new CustomException(msg, new Exception("Request failed."));
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Unexpected error: {ex}");
                                throw;
                            }

                            _logger.Trace("Retrieved data for cert with serial {0} from DigiCert.", kfserialnumber);
                        });
                        var newparseddigicertresponse =
                            JsonConvert.DeserializeObject<dynamic>(digicertnewlookupresponse.Content);


                        if (newparseddigicertresponse["page"]["total"] != 0)
                        {
                            var newflatteneddigicertinstance = newparseddigicertresponse["orders"][0];
                            var orderid = newflatteneddigicertinstance["id"].ToString();

                            var digicertmetadataupdateapilocation =
                                "https://www.digicert.com/services/v2/order/certificate/" + orderid + "/custom-field";
                            var digicertnewfieldsclient = new RestClient();
                            var digicertnewfieldsrequest = new RestRequest(digicertmetadataupdateapilocation);
                            digicertnewfieldsrequest.AddHeader("Accept", "application/json");
                            digicertnewfieldsrequest.AddHeader("X-DC-DEVKEY", digicertapikey);

                            foreach (var newfield in fullcustomdgfieldlist)
                            {
                                var keyfactorfieldname = "";
                                var datauploaded = false;
                                //Lookup the keyfactor name for digicert fields 
                                foreach (var sublookup in kfcustomfields)
                                    if (sublookup.DigicertFieldName == newfield.label)
                                    {
                                        var metadatapayload = new Dictionary<string, string>();
                                        metadatapayload["metadata_id"] = newfield.id.ToString();
                                        if (kfstoredmetadata.ContainsKey(sublookup.KeyfactorMetadataFieldName))
                                        {
                                            metadatapayload["value"] =
                                                kfstoredmetadata[sublookup.KeyfactorMetadataFieldName];
                                            var newserializedsyncfield = JsonConvert.SerializeObject(metadatapayload);
                                            digicertnewfieldsrequest.AddParameter("application/json",
                                                newserializedsyncfield, ParameterType.RequestBody);

                                            var digicertresponsenewfields = new RestResponse();
                                            GlobalRetryPolicy.RetryPolicy.Execute(() =>
                                            {
                                                try
                                                {
                                                    digicertresponsenewfields =
                                                        digicertnewfieldsclient.Post(digicertnewfieldsrequest);
                                                    if (!digicertresponsenewfields.IsSuccessful)
                                                    {
                                                        string msg =
                                                            "Something went wrong while updating metadata for cert" +
                                                            cert["SerialNumber"].ToString() + " in DigiCert.";
                                                        _logger.Error(msg);
                                                        throw new CustomException(msg,
                                                            new Exception("Request failed."));
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger.Error($"Unexpected error: {ex}");
                                                    throw;
                                                }

                                                _logger.Trace("Updated metadata for cert {0} in DigiCert.",
                                                    cert["SerialNumber"].ToString());
                                            });


                                            datauploaded = true;
                                        }
                                    }
                            }

                            numcertsdatauploaded += 1;
                        }
                    }

                    totalcertsprocessed += 1;
                }


                // Update the count of items downloaded so far
                certsdownloaded += batchsize;

                // Check if all items have been downloaded
                if (certlist.Count == 0)
                {
                    Console.WriteLine(
                        $"Metadata sync from Keyfactor to DigiCert complete. Number of certs processed: {totalcertsprocessed.ToString()}");
                    Console.WriteLine($"Certs that had their metadata synced: {numcertsdatauploaded.ToString()}");
                    _logger.Debug(
                        $"Metadata sync from Keyfactor to DigiCert complete. Number of certs processed: {totalcertsprocessed.ToString()}");
                    _logger.Debug($"Certs that had their metadata synced: {numcertsdatauploaded.ToString()}");
                    ;

                    break;
                }
            }
        }

        // Syncing Data from DigiCert TO Keyfactor
        if (config_mode == "dctokf")
        {
            // Initialize variable to keep track of items downloaded so far
            var certsdownloaded = 0;
            var certcounttracker = 0;
            for (var batchnum = 0; batchnum < numberOfBatches; batchnum++)
            {
                // Check if reaching the arbitrary limit
                if (certsdownloaded + batchsize > returnlimitint)
                {
                    Console.WriteLine($"Stopped downloading at the configured limit of {returnlimitint} items.");
                    _logger.Debug($"Stopped downloading at the configured limit of {returnlimitint} items.");
                    break;
                }

                // Download the items in this batch 
                Console.WriteLine($"Downloading batch {batchnum + 1}...");


                var digicertlookup = keyfactorapilocation + "Certificates?pq.queryString=IssuerDN%20-contains%20%22"
                                                          + digicertIssuerQueryterm + "%22&pq.returnLimit=" +
                                                          batchsize +
                                                          "&includeMetadata=true" + "&pq.pageReturned=" + batchnum;
                if (syncreissue) digicertlookup += "&pq.includeRevoked=true&pq.includeExpired=true";
                var request = new RestRequest(digicertlookup);
                request.AddHeader("Accept", "application/json");
                request.AddHeader("x-keyfactor-api-version", "1");
                request.AddHeader("x-keyfactor-requested-with", "APIClient");
                var keyfactorlookupResponse = new RestResponse();


                GlobalRetryPolicy.RetryPolicy.Execute(() =>
                {
                    try
                    {
                        keyfactorlookupResponse = kfclient.Execute(request);
                        if (!keyfactorlookupResponse.IsSuccessful)
                        {
                            var msg =
                                "Something went wrong while retrieving list of DigiCert issued certs from Keyfactor.";
                            _logger.Error(msg);
                            throw new CustomException(msg, new Exception("Request failed."));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Unexpected error: {ex}");
                        throw;
                    }

                    _logger.Debug("Got DigiCert issued certs from keyfactor");
                });

                var rawresponse = keyfactorlookupResponse.Content;
                var certlist = JsonConvert.DeserializeObject<List<KeyfactorCert>>(rawresponse,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Console.WriteLine("Got DigiCert issued certs from keyfactor");

                //Each cert that is DigiCert in origin in Keyfactor is looked up on DigiCert via serial number,
                //and the metadata contents from those fields are stored.
                var digicertlookupclient = new RestClient();
                var digicertcertificates = new List<dynamic>();
                foreach (var certinstance in certlist)
                {
                    // Use Order info endpoint; you can pass the primary cert serial number in the path.
                    // NOTE: This only works for the primary certificate on the order, not duplicates.
                    // Docs: GET /services/v2/order/certificate/{order_id|serial}
                    var digicertlookupurl =
                        $"https://www.digicert.com/services/v2/order/certificate/{certinstance.SerialNumber}";
                    var lookuprequest = new RestRequest(digicertlookupurl);
                    lookuprequest.AddHeader("Accept", "application/json");
                    lookuprequest.AddHeader("X-DC-DEVKEY", digicertapikey);

                    var digicertlookupresponse = new RestResponse();

                    GlobalRetryPolicy.RetryPolicy.Execute(() =>
                    {
                        try
                        {
                            digicertlookupresponse = digicertClient.Execute(lookuprequest);
                            if (!digicertlookupresponse.IsSuccessful)
                            {
                                var msg = $"DigiCert order lookup failed for serial {certinstance.SerialNumber}.";
                                _logger.Error(msg +
                                              $" HTTP {(int)digicertlookupresponse.StatusCode} {digicertlookupresponse.StatusDescription}. Body: {digicertlookupresponse.Content}");
                                throw new CustomException(msg, new Exception("Request failed."));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Unexpected error: {ex}");
                            throw;
                        }

                        _logger.Trace("Located order for cert serial {0} in DigiCert.", certinstance.SerialNumber);
                    });

                    // Parse as an order object (NOT just the 'certificate' object)
                    var order = JObject.Parse(digicertlookupresponse.Content);

                    // Serial number for matching (order info includes it)
                    var serial = order["certificate"]?["serial_number"]?.ToString()?.ToUpperInvariant();
                    if (string.IsNullOrEmpty(serial))
                    {
                        _logger.Trace("Order response missing certificate.serial_number for serial {0}",
                            certinstance.SerialNumber);
                        continue;
                    }

                    // Keep the whole order object; custom_fields are at the order level
                    digicertcertificates.Add(order);
                }


                Console.WriteLine("Pulled DigiCert matching DigiCert cert data.");
                _logger.Debug("Pulled DigiCert matching DigiCert cert data.");
                var finalsyncurl = keyfactorapilocation + "Certificates/Metadata";
                foreach (var digicertcertinstance in digicertcertificates)
                {
                    // Match Keyfactor cert
                    var test = digicertcertinstance["certificate"]?["serial_number"]?.ToString()?.ToUpperInvariant();
                    var certificateid = certlist.FirstOrDefault(k => k.SerialNumber == test)?.Id ?? 0;
                    if (certificateid == 0)
                    {
                        _logger.Trace("KF match not found for serial {0}", test);
                        continue;
                    }

                    var payloadforkf = new KeyfactorMetadataQuery { Id = certificateid };

                    // --- CUSTOM FIELDS ---
                    var customFields = digicertcertinstance["custom_fields"] as JArray;
                    if (customFields != null)
                        foreach (var cf in customFields)
                        {
                            var label = cf["label"]?.ToString();
                            var value = cf["value"]; // keep JToken: could be string/number/null

                            if (importallcustomdigicertfields)
                            {
                                // Auto-import: sanitize label for KF key
                                var metadatanamefield = ReplaceAllBannedCharacters(label ?? "", allBannedChars);
                                payloadforkf.Metadata[metadatanamefield] = value;
                            }
                            else
                            {
                                // Mapped import: use your mapping table
                                var mapping = kfcustomfields.FirstOrDefault(x => x.DigicertFieldName == label);
                                if (mapping != null)
                                    // BUGFIX: target should be the Keyfactor field name, not the DigiCert label
                                    payloadforkf.Metadata[mapping.KeyfactorMetadataFieldName] = value;
                            }
                        }

                    var flattenedcert = Flatten(digicertcertinstance);
                    //Getting manually selected metadata field values (not custom in DigiCert)
                    foreach (var manualinstance in kfmanualfields)
                        if (flattenedcert[manualinstance.DigicertFieldName] != null)
                            payloadforkf.Metadata[manualinstance.KeyfactorMetadataFieldName] =
                                flattenedcert[manualinstance.DigicertFieldName].ToString();
                    //Sending the payload off to Keyfactor for the update
                    var finalsyncreq = new RestRequest(finalsyncurl);
                    finalsyncreq.AddHeader("Content-Type", "application/json");
                    finalsyncreq.AddHeader("x-keyfactor-api-version", "1");
                    finalsyncreq.AddHeader("x-keyfactor-requested-with", "APIClient");
                    var serializedsyncfield = JsonConvert.SerializeObject(payloadforkf);
                    _logger.Trace("Sending Metadata update to KF for cert ID {0}, metadata update: {1}",
                        payloadforkf.Id.ToString(), serializedsyncfield);

                    finalsyncreq.AddParameter("application/json", serializedsyncfield, ParameterType.RequestBody);
                    var finalresponse = new RestResponse();

                    GlobalRetryPolicy.RetryPolicy.Execute(() =>
                    {
                        try
                        {
                            finalresponse = kfclient.Put(finalsyncreq);
                            if (!finalresponse.IsSuccessful)
                            {
                                string msg = "Something went wrong while submitting metadata update for cert " +
                                             digicertcertinstance["serial_number"] + " to Keyfactor.";
                                _logger.Error(msg);
                                throw new CustomException(msg, new Exception("Request failed."));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Unexpected error: {ex}");
                            throw;
                        }

                        string serial = digicertcertinstance["serial_number"]?.ToString() ?? "";
                        _logger.Trace("Submitted metadata update for cert {0} to Keyfactor.", serial); // OK
                    });
                    ++certcounttracker;
                }


                // Update the count of items downloaded so far
                certsdownloaded += batchsize;

                // Check if all items have been downloaded
                if (certlist.Count == 0)
                {
                    Console.WriteLine(
                        $"Metadata sync from Keyfactor to DigiCert complete. Number of certs synced: {certcounttracker.ToString()}");
                    _logger.Debug(
                        $"Metadata sync from Keyfactor to DigiCert complete. Number of certs synced: {certcounttracker.ToString()}");

                    break;
                }
            }
        }

        Environment.Exit(0);
    }

    public static class GlobalRetryPolicy
    {
        static GlobalRetryPolicy()
        {
            RetryPolicy = Policy
                .Handle<Exception>() // Handle all exceptions
                .Retry(5, (exception, retryCount, context) =>
                {
                    // Check if the exception is a CustomException
                    if (exception is CustomException customEx)
                        // Log the custom message from CustomException
                        _logger.Error($"Retry {retryCount} due to: {customEx.Message}");
                    else
                        // Log the message for other exceptions
                        _logger.Error($"Retry {retryCount} due to: {exception.Message}");
                });
        }

        public static Policy RetryPolicy { get; }
    }

    public class CustomException : Exception
    {
        public CustomException(string customMessage, Exception innerException = null)
            : base($"{customMessage} Original error: {innerException?.Message}", innerException)
        {
        }
    }
}