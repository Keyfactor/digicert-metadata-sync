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

using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Authenticators;

namespace DigicertMetadataSync;

// This fuction adds the fields to keyfactor.
// It will only add new fields.
partial class DigicertSync
{
    public static List<CustomDigicertMetadataInstance> GrabCustomFieldsFromDigiCert(string apikey)
    {
        var digicertclient = new RestClient();
        var customfieldsretrieval = "https://www.digicert.com/services/v2/account/metadata";
        var digicertrequest = new RestRequest(customfieldsretrieval);
        digicertrequest.AddHeader("Accept", "application/json");
        digicertrequest.AddHeader("X-DC-DEVKEY", apikey);
        var digicertresponse = digicertclient.Execute(digicertrequest);
        var trimmeddigicertresponse = digicertresponse.Content.Remove(0, 12);
        int lengthofresponse = trimmeddigicertresponse.Length;
        trimmeddigicertresponse = trimmeddigicertresponse.Remove(lengthofresponse - 1, 1);
        var fieldlist = JsonConvert.DeserializeObject<List<CustomDigicertMetadataInstance>>(trimmeddigicertresponse);
        Console.WriteLine("Obtained custom fields from DigiCert.");
        _logger.Debug("Obtained custom fields from DigiCert.");
        return fieldlist;
    }
}