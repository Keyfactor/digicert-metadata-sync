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

namespace DigicertMetadataSync;

partial class DigicertSync
{

    public class DigicertCustomFieldInstance
    {
        public int id { get; set; } = 999999999;
        public string label { get; set; } = "";
        public bool is_required { get; set; } = false;
        public bool is_active { get; set; } = true;
        public string data_type { get; set; } = "anything";
        public string kf_field_name { get; set; } = "";
    }

    public class DigicertMetadataUpdateInstance
    {
        public int metadata_id { get; set; } = 999999999;
        public string value { get; set; } = "false";    
    }

}