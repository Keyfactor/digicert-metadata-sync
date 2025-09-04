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

// using Newtonsoft.Json;  // already present in your project

public class KeyfactorMetadataInstance
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