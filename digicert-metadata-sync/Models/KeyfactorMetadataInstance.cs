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
    //This stores all of the data keyfactor API returns when asked for metadata field details.
    public class KeyfactorMetadataInstance
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int DataType { get; set; }
        public string Hint { get; set; }
        public string Validation { get; set; }
        public int Enrollment { get; set; }
        public string Message { get; set; }
        public string Options { get; set; }
        public string DefaultValue { get; set; }
        public bool AllowAPI { get; set; }
        public bool ExplicitUpdate { get; set; }
        public int DisplayOrder { get; set; }
    }
}