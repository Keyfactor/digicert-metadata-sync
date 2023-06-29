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

internal partial class DigicertSync
{
    public class CustomDigicertMetadataInstance
    {
        public int id { get; set; }
        public string label { get; set; }
        public bool is_required { get; set; }
        public bool is_active { get; set; }
        public string description { get; set; }
        public string data_type { get; set; }
    }

    public class ReadInMetadataField
    {
        public string DigicertFieldName { get; set; } = "local_test_nullx0";
        public string KeyfactorMetadataFieldName { get; set; } = "test_name_nullx0";
        public string KeyfactorDescription { get; set; } = "None.";
        public string KeyfactorDataType { get; set; } = "string";
        public string KeyfactorHint { get; set; } = "None.";
        public string KeyfactorAllowAPI { get; set; } = "True";
    }

    public class KeyfactorMetadataInstanceSendoff
    {
        public string Name { get; set; } = "";

        public string Description { get; set; } = "No description provided.";

        //Default field type is set to 1 for Keyfactor - string
        public int DataType { get; set; } = 1;
        public string Hint { get; set; } = "";
        public bool AllowAPI { get; set; } = true;
    }
}