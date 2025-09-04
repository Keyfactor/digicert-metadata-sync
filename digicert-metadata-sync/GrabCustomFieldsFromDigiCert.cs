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

using Newtonsoft.Json.Linq;
using RestSharp;

namespace DigicertMetadataSync;

// This fuction adds the fields to keyfactor.
// It will only add new fields.
internal partial class DigicertSync
{
    public static List<CustomDigicertMetadataInstance> GrabCustomFieldsFromDigiCert(
        string apikey, bool importdeactivated, RestClient digicertClient)
    {
        const string url = "https://www.digicert.com/services/v2/account/metadata";

        var req = new RestRequest(url);
        req.AddHeader("Accept", "application/json");
        req.AddHeader("Content-Type", "application/json"); // matches DigiCert examples
        req.AddHeader("X-DC-DEVKEY", apikey);

        var resp = new RestResponse();
        GlobalRetryPolicy.RetryPolicy.Execute(() =>
        {
            try
            {
                resp = digicertClient.Execute(req);
                if (!resp.IsSuccessful)
                {
                    var msg = "Something went wrong while retrieving custom fields from DigiCert.";
                    _logger.Error(msg + $" HTTP {(int)resp.StatusCode} {resp.StatusDescription}. Body: {resp.Content}");
                    throw new CustomException(msg, new Exception("Request failed."));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected error: {ex}");
                throw;
            }

            _logger.Debug("Obtained custom fields from DigiCert.");
        });

        // Robust JSON handling per spec:
        // { "metadata": [ ... ] }  OR  {} when none
        var fieldlist = new List<CustomDigicertMetadataInstance>();
        if (!string.IsNullOrWhiteSpace(resp.Content))
        {
            var root = JObject.Parse(resp.Content);
            var metaToken = root["metadata"];
            if (metaToken != null && metaToken.Type == JTokenType.Array)
                fieldlist = metaToken.ToObject<List<CustomDigicertMetadataInstance>>() ??
                            new List<CustomDigicertMetadataInstance>();
            // else {} --> leave fieldlist empty
        }

        if (!importdeactivated) fieldlist.RemoveAll(f => f.is_active == false);

        Console.WriteLine("Obtained custom fields from DigiCert.");
        return fieldlist;
    }
}