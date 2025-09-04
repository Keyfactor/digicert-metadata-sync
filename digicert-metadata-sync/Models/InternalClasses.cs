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


    // Add or replace with this fuller version
    public class ReadInMetadataField
    {
        public string DigicertFieldName { get; set; } = "local_test_nullx0";
        public string KeyfactorMetadataFieldName { get; set; } = "test_name_nullx0";
        public string KeyfactorDescription { get; set; } = "None.";

        public string KeyfactorDataType { get; set; } =
            "String"; // String, Integer, Date, Boolean, Multiple Choice, Big Text

        public string KeyfactorHint { get; set; } = "";
        public string KeyfactorAllowAPI { get; set; } = "True";

        // NEW: keep config explicit and self-contained
        public string KeyfactorValidation { get; set; } = ""; // regex (String fields only)
        public string KeyfactorMessage { get; set; } = ""; // message for failed regex
        public string KeyfactorEnrollment { get; set; } = "Optional"; // Optional | Required | Hidden
        public List<string> KeyfactorOptions { get; set; } = new(); // Multiple Choice values
        public string KeyfactorDefaultValue { get; set; } = ""; // default value
        public int? KeyfactorDisplayOrder { get; set; } = null; // if null, we’ll set later
        public string FieldType { get; set; } = "manual/custom"; // existing behavior
    }

    // using Newtonsoft.Json;  // already present in your project

    public class KeyfactorMetadataInstanceSendoff
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "No description provided.";
        public int DataType { get; set; } = 1; // 1=String, 2=Integer, 3=Date, 4=Boolean, 5=Multiple Choice, 6=Big Text
        public string Hint { get; set; } = "";

        // (keep these if you use them)
        public string Validation { get; set; } = "";
        public int Enrollment { get; set; } = 0; // 0 Optional, 1 Required, 2 Hidden
        public string Message { get; set; } = "";

        // IMPORTANT: Options is always a CSV string (never an array)
        public string Options { get; set; } = "";

        public string DefaultValue { get; set; } = "";

        // Deprecated but harmless if present
        public bool AllowAPI { get; set; } = true;
    }
}