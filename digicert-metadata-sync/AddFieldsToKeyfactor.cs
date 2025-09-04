// Copyright 2021 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Newtonsoft.Json;
using RestSharp;

namespace DigicertMetadataSync;

// This fuction adds the fields to keyfactor.
// It will only add new fields.
internal partial class DigicertSync
{
    public static Tuple<int, List<string>> AddFieldsToKeyfactor(List<KeyfactorMetadataInstanceSendoff> inputlist,
        List<KeyfactorMetadataInstance> existingmetadatalist, bool noexistingfields, string keyfactorusername,
        string keyfactorpassword, string keyfactorapilocation, RestClient kfClient)
    {
        var addfieldstokeyfactorurl = keyfactorapilocation + "MetadataFields";
        var totalnumberadded = 0;
        var newfields = new List<string>();
        if (inputlist.Count != 0)
            foreach (var metadatainstance in inputlist)
                if (noexistingfields == false)
                {
                    var fieldquery = from existingmetadatainstance in existingmetadatalist
                        where existingmetadatainstance.Name == metadatainstance.Name
                        select existingmetadatainstance;
                    // If field does not exist in Keyfactor, add it.
                    if (!fieldquery.Any())
                    {
                        // Ensure DisplayOrder is set and Options are in the expected format per latest API.
                        try
                        {
                            // API expects array for multiple choice; make sure it's not null.

                            // Ensure Options is a string (always present)
                            if (metadatainstance.Options == null)
                                metadatainstance.Options = string.Empty;
                        }
                        catch
                        {
                            /* non-fatal; continue with best effort */
                        }

                        var addfieldrequest = new RestRequest(addfieldstokeyfactorurl);
                        addfieldrequest.AddHeader("Content-Type", "application/json");
                        addfieldrequest.AddHeader("Accept", "application/json");
                        addfieldrequest.AddHeader("x-keyfactor-api-version", "1");
                        addfieldrequest.AddHeader("x-keyfactor-requested-with", "APIClient");
                        var serializedfield = JsonConvert.SerializeObject(metadatainstance);
                        addfieldrequest.AddParameter("application/json", serializedfield, ParameterType.RequestBody);
                        var metadataresponse = new RestResponse();
                        GlobalRetryPolicy.RetryPolicy.Execute(() =>
                        {
                            try
                            {
                                metadataresponse = kfClient.Post(addfieldrequest);
                                ;
                                if (!metadataresponse.IsSuccessful)
                                {
                                    var msg = "Something went wrong while adding field " + metadatainstance.Name +
                                              " to Keyfactor.";
                                    _logger.Error(msg);
                                    throw new CustomException(msg, new Exception("Request failed."));
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"Unexpected error: {ex}");
                                throw;
                            }

                            _logger.Trace("Added field {0} to Keyfactor.", metadatainstance.Name);
                        });
                        newfields.Add(metadatainstance.Name);
                        ++totalnumberadded;
                    }
                    else
                    {
                        if (fieldquery.FirstOrDefault().DataType != metadatainstance.DataType)
                        {
                            //Throw error if datatype included in keyfactor does not match the digicert one.
                            var mismatchedtypes = new NotSupportedException();
                            throw mismatchedtypes;
                        }
                    }
                }
                else
                {
                    var addfieldrequest = new RestRequest(addfieldstokeyfactorurl);
                    addfieldrequest.AddHeader("Content-Type", "application/json");
                    addfieldrequest.AddHeader("Accept", "application/json");
                    addfieldrequest.AddHeader("x-keyfactor-api-version", "1");
                    addfieldrequest.AddHeader("x-keyfactor-requested-with", "APIClient");
                    var serializedfield = JsonConvert.SerializeObject(metadatainstance);
                    addfieldrequest.AddParameter("application/json", serializedfield, ParameterType.RequestBody);
                    var metadataresponse = new RestResponse();

                    GlobalRetryPolicy.RetryPolicy.Execute(() =>
                    {
                        try
                        {
                            metadataresponse = kfClient.Post(addfieldrequest);
                            ;
                            if (!metadataresponse.IsSuccessful)
                            {
                                var msg = "Something went wrong while adding field " + metadatainstance.Name +
                                          " to Keyfactor.";
                                _logger.Error(msg);
                                throw new CustomException(msg, new Exception("Request failed."));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Unexpected error: {ex}");
                            throw;
                        }

                        _logger.Trace("Added field {0} to Keyfactor.", metadatainstance.Name);
                    });
                    ++totalnumberadded;
                }

        var returnvals = new Tuple<int, List<string>>(totalnumberadded, newfields);

        return returnvals;
    }
}