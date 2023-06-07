// Copyright 2021 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.


using System;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using RestSharp;
using RestSharp.Authenticators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System;
using System.Configuration;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.Collections.Specialized;
using System.Text.Json.Serialization.Metadata;
using Keyfactor;
using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using static DigicertMetadataSync.DigicertSync;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace DigicertMetadataSync
{
    partial class DigicertSync
    {

        public static void Main(string[] args)
        {
            ILogger logger = LogHandler.GetClassLogger<DigicertSync>();
            logger.LogDebug("Start sync");
            var digicertapikey = System.Configuration.ConfigurationManager.AppSettings.Get("DigicertAPIKey");
            var digicertapikeytopperm = System.Configuration.ConfigurationManager.AppSettings.Get("DigicertAPIKeyTopPerm");
            var keyfactorusername = System.Configuration.ConfigurationManager.AppSettings.Get("KeyfactorDomainAndUser");
            var keyfactorpassword = System.Configuration.ConfigurationManager.AppSettings.Get("KeyfactorPassword");
            var replacementcharacter = System.Configuration.ConfigurationManager.AppSettings.Get("ReplaceDigicertWhiteSpaceCharacterInName");
            var importallcustomdigicertfields = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings.Get("ImportAllCustomDigicertFields"));

            var config_mode = args[0];
            if (CheckMode(config_mode) == false)
            {
                logger.LogDebug("Inappropriate configuration mode. Check your command line arguments.");
                throw new Exception("Inappropriate configuration mode. Check your command line arguments.");
            }

            //Get list of all DigiCert certs from Keyfactor based on query that contains DigiCert as issuer.
            var returnlimit = System.Configuration.ConfigurationManager.AppSettings.Get("KeyfactorCertSearchReturnLimit").ToString();
            var keyfactorapilocation = System.Configuration.ConfigurationManager.AppSettings.Get("KeyfactorAPIEndpoint").ToString();
            var digicertIssuerQueryterm = System.Configuration.ConfigurationManager.AppSettings.Get("KeyfactorDigicertIssuedCertQueryTerm").ToString();
            logger.LogDebug($"Loaded config. Processing with a Keyfactor Query Return Limit of {returnlimit.ToString()}");

            var digicertlookup = keyfactorapilocation + "Certificates?pq.queryString=IssuerDN%20-contains%20%22"
                    + digicertIssuerQueryterm + "%22&pq.returnLimit=" + returnlimit + "&includeMetadata=true";
            var client = new RestClient();
            client.Authenticator = new HttpBasicAuthenticator(keyfactorusername, keyfactorpassword);
            var request = new RestRequest(digicertlookup);
            request.AddHeader("Accept", "application/json");
            request.AddHeader("x-keyfactor-api-version", "1");
            request.AddHeader("x-keyfactor-requested-with", "APIClient");
            var response = client.Execute(request);
            var rawresponse = response.Content;
            var certlist = JsonConvert.DeserializeObject<List<KeyfactorCert>>(rawresponse, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Console.WriteLine("Got DigiCert issued certs from keyfactor");
            logger.LogDebug("Got DigiCert issued certs from keyfactor");

            //Getting list of custom metadata fields from Keyfactor
            var getmetadalistkf = keyfactorapilocation + "MetadataFields";
            var getmetadatakfclient = new RestClient();
            getmetadatakfclient.Authenticator = new HttpBasicAuthenticator(keyfactorusername, keyfactorpassword);
            var metadatakfrequest = new RestRequest(getmetadalistkf);
            metadatakfrequest.AddHeader("Accept", "application/json");
            metadatakfrequest.AddHeader("x-keyfactor-api-version", "1");
            metadatakfrequest.AddHeader("x-keyfactor-requested-with", "APIClient");
            var metadatakfresponse = client.Execute(metadatakfrequest);
            var metadatakfrawresponse = metadatakfresponse.Content;
            var kfmetadatafields = JsonConvert.DeserializeObject<List<KeyfactorMetadataInstance>>(metadatakfrawresponse);
            Console.WriteLine("Got list of custom fields from Keyfactor.");
            logger.LogDebug("Got list of custom fields from Keyfactor.");

            //Getting list of custom metadata fields on DigiCert
            var customdigicertmetadatafieldlist = GrabCustomFieldsFromDigiCert(digicertapikey);

            //Convert DigiCert custom fields to Keyfactor appropriate ones
            //This depends on whether the setting to import all fields was enabled or not

            var config = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("manualfields.json").Build();
            var kfcustomfields = new List<ReadInMetadataField>();
            if (importallcustomdigicertfields == true)
            {
                //This imports all the custom fields based on the list of metadata from DigiCert and does autofill
                for (int i = 0; i < customdigicertmetadatafieldlist.Count; i++)
                {
                    var localkffieldinstance = new ReadInMetadataField();
                    var kfdatatype = "String";
                    if (customdigicertmetadatafieldlist[i].data_type != null)
                    {
                        localkffieldinstance.KeyfactorDataType = customdigicertmetadatafieldlist[i].data_type;
                    }
                    else
                    {
                        localkffieldinstance.KeyfactorDataType = "String";
                    }
                    if (customdigicertmetadatafieldlist[i].label != null)
                    {
                        /*
                            NOTICE: KEYFACTOR DOES NOT SUPPORT SPACES IN METADATA FIELD NAMES.
                        WHITESPACE MUST BE REMOVED FROM THE NAME.
                        CURRENTLY REPLACING WITH "_-_" AS STAND IN FOR SPACE CHARACTER.
                            */
                        localkffieldinstance.DigicertFieldName = customdigicertmetadatafieldlist[i].label;
                        localkffieldinstance.KeyfactorMetadataFieldName = ReplaceAllWhiteSpaces(customdigicertmetadatafieldlist[i].label, replacementcharacter);
                    }
                    else
                    {
                        localkffieldinstance.DigicertFieldName = "";
                        localkffieldinstance.KeyfactorMetadataFieldName = "";
                    }
                    if (customdigicertmetadatafieldlist[i].description != null)
                    {
                        localkffieldinstance.KeyfactorDescription = customdigicertmetadatafieldlist[i].description;
                    }
                    else
                    {
                        localkffieldinstance.KeyfactorDescription = "None.";
                    }

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
                if (kfcustomfields == null)
                {
                    kfcustomfields = new List<ReadInMetadataField>();
                }
            }

            //Adding metadata fields for the ID and the email of the requester from DigiCert.
            List<ReadInMetadataField> kfmanualfields = new List<ReadInMetadataField>();
            var manualfieldslist = "ManualFields";
            kfmanualfields = config.GetSection(manualfieldslist).Get<List<ReadInMetadataField>>();
            if (kfmanualfields == null)
            {
                kfmanualfields = new List<ReadInMetadataField>();
            }
            logger.LogDebug("Performed field conversion.");

            //Pulling list of existing metadata fields from Keyfactor for later comparison.
            bool noexistingfields = true;

            var existingmetadataurl = keyfactorapilocation + "MetadataFields";
            var existingmetadataclient = new RestClient();
            existingmetadataclient.Authenticator = new HttpBasicAuthenticator(keyfactorusername, keyfactorpassword);
            var existingmetadatareq = new RestRequest(existingmetadataurl);
            existingmetadatareq.AddHeader("Accept", "application/json");
            existingmetadatareq.AddHeader("x-keyfactor-api-version", "1");
            existingmetadatareq.AddHeader("x-keyfactor-requested-with", "APIClient");
            var existingmetadataresponse = existingmetadataclient.Execute(existingmetadatareq);
            List<KeyfactorMetadataInstance> existingmetadatalist = new List<KeyfactorMetadataInstance>();
            if (existingmetadataresponse != null)
            {
                //Fields exist
                existingmetadatalist = JsonConvert.DeserializeObject<List<KeyfactorMetadataInstance>>(existingmetadataresponse.Content);
                noexistingfields = false;
            }
            Console.WriteLine("Pulled existing metadata fields from keyfactor.");
            logger.LogDebug("Pulled existing metadata fields from Keyfactor.");
            // Converting the read in fields into sendable lists
            var convertedmanualfields = convertlisttokf(kfmanualfields, replacementcharacter);
            var convertedcustomfields = convertlisttokf(kfcustomfields, replacementcharacter);

            int totalfieldsadded = 0;

            //If all the fields are absent from Keyfactor, the fields are added.
            var manualresult = AddFieldsToKeyfactor(convertedmanualfields, existingmetadatalist, noexistingfields, keyfactorusername, keyfactorpassword, keyfactorapilocation);
            var customresult = AddFieldsToKeyfactor(convertedcustomfields, existingmetadatalist, noexistingfields, keyfactorusername, keyfactorpassword, keyfactorapilocation);

            totalfieldsadded += manualresult.Item1;
            totalfieldsadded += customresult.Item1;

            var allnewfields = manualresult.Item2.Concat(customresult.Item2).ToList(); 
            // Syncing Data from Keyfactor TO DigiCert
            // Sync from DigiCert to Keyfactor must run at least once prior to this - only runs with custom fields
            if (config_mode == "kftodc")
            {
                Console.WriteLine($"Added custom fields to Keyfactor. Total fields added: {totalfieldsadded.ToString()}");
                logger.LogDebug($"Added custom fields to Keyfactor. Total fields added: {totalfieldsadded.ToString()}");

                List<DigicertCustomFieldInstance> fullcustomdgfieldlist = new List<DigicertCustomFieldInstance>();
                List<DigicertCustomFieldInstance> newcustomfieldsfordg = new List<DigicertCustomFieldInstance>();
                // Rebuild the list of metadata field names as they are on DigiCerts side. 
              
                // This covers all of the custom fields on Digicerts side
                foreach (var dgcustomfield in customdigicertmetadatafieldlist)
                {
                    DigicertCustomFieldInstance localdigicertfieldinstance = new DigicertCustomFieldInstance();
                    
                    localdigicertfieldinstance.label = dgcustomfield.label;
                    localdigicertfieldinstance.is_active = dgcustomfield.is_active;
                    localdigicertfieldinstance.data_type = dgcustomfield.data_type;
                    localdigicertfieldinstance.is_required = dgcustomfield.is_required;
                    
                    foreach (var kffieldeq in kfcustomfields)
                    {
                        if (dgcustomfield.label == kffieldeq.DigicertFieldName)
                        {
                            localdigicertfieldinstance.kf_field_name = kffieldeq.DigicertFieldName;
                        }
                    }

                    fullcustomdgfieldlist.Add(localdigicertfieldinstance);
                }

                
                //This covers all of the new fields on Keyfactors side, including new ones - needs to have digicert ids for the new ones
                foreach (var kfcustomfield in kfcustomfields)
                {
                    DigicertCustomFieldInstance localdigicertfieldinstance = new DigicertCustomFieldInstance();
                    localdigicertfieldinstance.label = kfcustomfield.DigicertFieldName;
                    localdigicertfieldinstance.is_active = true;
                    localdigicertfieldinstance.kf_field_name = kfcustomfield.KeyfactorMetadataFieldName;
                    if (kfcustomfield.KeyfactorDataType == "String")
                    {
                        localdigicertfieldinstance.data_type = "text";
                    }
                    else if (kfcustomfield.KeyfactorDataType == "Int")
                    {
                        localdigicertfieldinstance.data_type = "int";
                    }
                    else
                    {
                        localdigicertfieldinstance.data_type = "anything";
                    }
                    localdigicertfieldinstance.is_required = false;
       
                    if (!fullcustomdgfieldlist.Any(p => p.label == localdigicertfieldinstance.label))
                    {
                        fullcustomdgfieldlist.Add(localdigicertfieldinstance);
                        newcustomfieldsfordg.Add(localdigicertfieldinstance);
                    }
                }

                //Add fields that don't exist on DigiCert to Digicert 
                foreach (var newdgfield in newcustomfieldsfordg)
                {
                    var digicertapilocation = "https://www.digicert.com/services/v2/account/metadata";
                    var digicertnewfieldsclient = new RestClient();
                    var digicertnewfieldsrequest = new RestRequest(digicertapilocation);
                    digicertnewfieldsrequest.AddHeader("Accept", "application/json");
                    digicertnewfieldsrequest.AddHeader("X-DC-DEVKEY", digicertapikeytopperm);
                    var serializedsyncfield = JsonConvert.SerializeObject(newdgfield);
                    digicertnewfieldsrequest.AddParameter("application/json", serializedsyncfield, ParameterType.RequestBody);
                    var digicertresponsenewfields = digicertnewfieldsclient.Post(digicertnewfieldsrequest);

                }


                // Grabbing the list again from digicert, populating ids for new ones 
                //Getting list of custom metadata fields on DigiCert
                var updatedmetadatafieldlist = GrabCustomFieldsFromDigiCert(digicertapikey);
                foreach (var subitem in updatedmetadatafieldlist)
                {
                    foreach (var fulllistitem in fullcustomdgfieldlist)
                    {
                        if (subitem.label == fulllistitem.label)
                        {
                            fulllistitem.id = subitem.id;
                        }
                    }
                   
                }

                var totalcertsprocessed = 0;
                var numcertsdatauploaded = 0;
                
                // Pushing the data to DigiCert
                var certlist2 = JsonConvert.DeserializeObject<dynamic>(rawresponse, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                foreach (var cert in certlist2)
                {

                    Dictionary<string, string> kfstoredmetadata = cert["Metadata"].ToObject<Dictionary<string, string>>();

                    bool certhascustomfields = false;
                    foreach (var checkfield in fullcustomdgfieldlist)
                    {
                        if (kfstoredmetadata.ContainsKey(checkfield.kf_field_name))
                        {
                            certhascustomfields = true;
                        }
                    }

                    if (certhascustomfields){
                        var kfserialnumber = cert["SerialNumber"].ToString();

                        var digicertnewlookupurl = "https://www.digicert.com/services/v2/order/certificate" + "?filters[serial_number]=" + kfserialnumber;

                        var newbodytemplate = new RootDigicertLookup();
                        var newsearchcriterioninstance = new SearchCriterion();
                        newbodytemplate.searchCriteriaList.Add(newsearchcriterioninstance);
                        var lookupnewrequest = new RestRequest(digicertnewlookupurl);
                        lookupnewrequest.AddHeader("Content-Type", "application/json");
                        lookupnewrequest.AddHeader("X-DC-DEVKEY", digicertapikey);
                        var digicertnewlookupresponse = client.Execute(lookupnewrequest);
                        var newparseddigicertresponse = JsonConvert.DeserializeObject<dynamic>(digicertnewlookupresponse.Content);


                        if (newparseddigicertresponse["page"]["total"] != 0)
                        {
                            var newflatteneddigicertinstance = newparseddigicertresponse["orders"][0];
                            var orderid = newflatteneddigicertinstance["id"].ToString();

                            var digicertmetadataupdateapilocation = "https://www.digicert.com/services/v2/order/certificate/" + orderid + "/custom-field";
                            var digicertnewfieldsclient = new RestClient();
                            var digicertnewfieldsrequest = new RestRequest(digicertmetadataupdateapilocation);
                            digicertnewfieldsrequest.AddHeader("Accept", "application/json");
                            digicertnewfieldsrequest.AddHeader("X-DC-DEVKEY", digicertapikey);

                            foreach (var newfield in fullcustomdgfieldlist)
                            {
                                string keyfactorfieldname = "";
                                bool datauploaded = false;
                                //Lookup the keyfactor name for digicert fields 
                                foreach (var sublookup in kfcustomfields)
                                {
                                    if (sublookup.DigicertFieldName == newfield.label)
                                    {
                                        Dictionary<string, string> metadatapayload = new Dictionary<string, string>();
                                        metadatapayload["metadata_id"] = newfield.id.ToString();
                                        if (kfstoredmetadata.ContainsKey(sublookup.KeyfactorMetadataFieldName))
                                        {
                                            metadatapayload["value"] = kfstoredmetadata[sublookup.KeyfactorMetadataFieldName];
                                            var newserializedsyncfield = JsonConvert.SerializeObject(metadatapayload);
                                            digicertnewfieldsrequest.AddParameter("application/json", newserializedsyncfield, ParameterType.RequestBody);
                                            var digicertresponsenewfields = digicertnewfieldsclient.Post(digicertnewfieldsrequest);
                                            datauploaded = true;
                                        }
                                        
                                    }
                                }
                            }
                            numcertsdatauploaded += 1;
                        }
                    }
                    totalcertsprocessed += 1;
                }
                Console.WriteLine($"Metadata sync from Keyfactor to DigiCert complete. Number of certs processed: {totalcertsprocessed.ToString()}");
                Console.WriteLine($"Certs that had their metadata synced: {numcertsdatauploaded.ToString()}");
                logger.LogDebug($"Metadata sync from Keyfactor to DigiCert complete. Number of certs processed: {totalcertsprocessed.ToString()}");
                logger.LogDebug($"Certs that had their metadata synced: {numcertsdatauploaded.ToString()}");

            }

            // Syncing Data from DigiCert TO Keyfactor
            if (config_mode == "dctokf")
            {
                Console.WriteLine($"Added custom fields to Keyfactor. Total fields added: {totalfieldsadded.ToString()}");
                logger.LogDebug($"Added custom fields to Keyfactor. Total fields added: {totalfieldsadded.ToString()}");
                //Each cert that is DigiCert in origin in Keyfactor is looked up on DigiCert via serial number,
                //and the metadata contents from those fields are stored.
                var digicertlookupclient = new RestClient();
                List<DigicertCert> digicertcertificates = new List<DigicertCert>();
                foreach (var certinstance in certlist)
                {
                    var digicertlookupurl = "https://www.digicert.com/services/v2/order/certificate/";

                    var bodytemplate = new RootDigicertLookup();
                    var searchcriterioninstance = new SearchCriterion();
                    bodytemplate.searchCriteriaList.Add(searchcriterioninstance);

                    digicertlookupurl = digicertlookupurl + certinstance.SerialNumber;
                    var lookuprequest = new RestRequest(digicertlookupurl);
                    lookuprequest.AddHeader("Content-Type", "application/json");
                    lookuprequest.AddHeader("X-DC-DEVKEY", digicertapikey);
                    var digicertlookupresponse = client.Execute(lookuprequest);
                    var parseddigicertresponse = JsonConvert.DeserializeObject<DigicertCert>(digicertlookupresponse.Content);
                    if (parseddigicertresponse.certificate != null)
                    {
                        var flatteneddigicertinstance = ClassConverter(parseddigicertresponse);
                        digicertcertificates.Add(parseddigicertresponse);
                    }
                }

                Console.WriteLine("Pulled DigiCert matching DigiCert cert data.");
                logger.LogDebug("Pulled DigiCert matching DigiCert cert data.");
                

                int certcounttracker = 0;
                foreach (var digicertcertinstance in digicertcertificates)
                {
                    var finalsyncclient = new RestClient();
                    finalsyncclient.Authenticator = new HttpBasicAuthenticator(keyfactorusername, keyfactorpassword);
                    var finalsyncurl = keyfactorapilocation + "Certificates/Metadata";
                    //Find matching certificate via Keyfactor ID
                    var query = from kfcertlocal in certlist
                                where kfcertlocal.SerialNumber ==
                                digicertcertinstance.certificate.serial_number.ToUpper()
                                select kfcertlocal;
                    var certificateid = query.FirstOrDefault().Id;


                    var payloadforkf = new KeyfactorMetadataQuery();
                    payloadforkf.Id = certificateid;

                    if (digicertcertinstance.custom_fields != null)
                    {
                        // Getting custom metadata field values
                        foreach (var metadatafieldinstance in digicertcertinstance.custom_fields)
                        {
                            if (importallcustomdigicertfields == true)
                            {
                                // Using autoimport and thus using autorename
                                payloadforkf.Metadata[ReplaceAllWhiteSpaces(metadatafieldinstance.label, replacementcharacter)] = metadatafieldinstance.value;
                            }
                            else
                            {
                                //Using custom names
                                var metadatanamequery = from customfieldinstance in kfcustomfields
                                                        where customfieldinstance.DigicertFieldName ==
                                                        metadatafieldinstance.label
                                                        select customfieldinstance;
                                if (metadatanamequery.FirstOrDefault() != null)
                                {
                                    payloadforkf.Metadata[metadatanamequery.FirstOrDefault().DigicertFieldName] = metadatafieldinstance.value;
                                }

                            }

                        }
                    }

                    var flatteneddigicertinstance = ClassConverter(digicertcertinstance);

                    //Getting manually selected metadata field values (not custom in DigiCert)
                    foreach (var manualinstance in kfmanualfields)
                    {
                        string[] access = manualinstance.DigicertFieldName.Split(".");

                        List<string> keys = access.ToList();
                        Dictionary<string, object> recursionresult = recursiveopener(flatteneddigicertinstance, keys, keys.Count);
                        object value = new object();
                        if (recursionresult != null)
                        {
                            value = recursionresult.First().Value;
                        }
                        else
                        {
                            value = "";
                        }
                        payloadforkf.Metadata[manualinstance.KeyfactorMetadataFieldName] = value;
                    }
                    payloadforkf.Metadata["DigicertID"] = digicertcertinstance.id.ToString();
                    //Sending the payload off to Keyfactor for the update
                    var finalsyncreq = new RestRequest(finalsyncurl);
                    finalsyncreq.AddHeader("Content-Type", "application/json");
                    finalsyncreq.AddHeader("x-keyfactor-api-version", "1");
                    finalsyncreq.AddHeader("x-keyfactor-requested-with", "APIClient");
                    var serializedsyncfield = JsonConvert.SerializeObject(payloadforkf);
                    finalsyncreq.AddParameter("application/json", serializedsyncfield, ParameterType.RequestBody);
                    finalsyncclient.Put(finalsyncreq);
                    ++certcounttracker;
                }

                Console.WriteLine($"Metadata sync from Keyfactor to DigiCert complete. Number of certs synced: {certcounttracker.ToString()}");
                logger.LogDebug($"Metadata sync from Keyfactor to DigiCert complete. Number of certs synced: {certcounttracker.ToString()}");
            }
        }
    }
}


