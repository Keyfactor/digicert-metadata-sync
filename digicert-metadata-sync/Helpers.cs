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

using System.Configuration;
using Newtonsoft.Json.Linq;

namespace DigicertMetadataSync;

internal partial class DigicertSync
{
    public static int TypeMatcher(string keyfactorType)
    {
        if (string.IsNullOrWhiteSpace(keyfactorType)) return 1;
        var t = keyfactorType.Trim().ToLowerInvariant();

        // canonical names + common aliases
        return t switch
        {
            "string" or "str" or "text" => 1,
            "int" or "integer" => 2,
            "date" or "datetime" => 3,
            "bool" or "boolean" => 4,
            "multiple choice" or "multiplechoice" or "choice" or "dropdown" => 5,
            "big text" or "bigtext" or "multiline" => 6,
            _ => 1
        };
    }
}

internal partial class DigicertSync
{
    public static Dictionary<string, object> ClassConverter(object obj)
    {
        if (obj != null && obj != "")
        {
            var resultdict = new Dictionary<string, object>();
            var propertylist = obj.GetType().GetProperties();

            foreach (var prop in propertylist)
            {
                var propName = prop.Name;
                var val = obj.GetType().GetProperty(propName).GetValue(obj, null);
                if (val != null)
                    resultdict.Add(propName, val);
                else
                    resultdict.Add(propName, "");
            }

            return resultdict;
        }

        return null;
    }

    public static string ReplaceAllBannedCharacters(string input, List<CharDBItem> allBannedChars)
    {
        foreach (var item in allBannedChars) input = input.Replace(item.character, item.replacementcharacter);
        return input;
    }

    public static bool CheckMode(string mode)
    {
        if (mode == "kftodc" || mode == "dctokf") return true;
        return false;
    }

    private static List<KeyfactorMetadataInstanceSendoff> convertlisttokf(
        List<ReadInMetadataField> inputlist,
        List<CharDBItem> allBannedChars,
        bool importallcustomfields)
    {
        var formattedlist = new List<KeyfactorMetadataInstanceSendoff>();
        if (inputlist == null || inputlist.Count == 0) return formattedlist;

        foreach (var input in inputlist)
        {
            var formatinstance = new KeyfactorMetadataInstanceSendoff();

            // Name: prefer user override; else clean DigiCert name
            formatinstance.Name = string.IsNullOrWhiteSpace(input.KeyfactorMetadataFieldName) ||
                                  input.FieldType == "Custom"
                ? ReplaceAllBannedCharacters(input.DigicertFieldName, allBannedChars)
                : input.KeyfactorMetadataFieldName;

            formatinstance.AllowAPI = SafeToBool(input.KeyfactorAllowAPI, true);
            formatinstance.Hint = input.KeyfactorHint ?? "";
            formatinstance.DataType = TypeMatcher(input.KeyfactorDataType);
            formatinstance.Description = input.KeyfactorDescription ?? "";

            // ALWAYS build a CSV string (even for non-Multiple Choice types)
            var optionsList = input.KeyfactorOptions ?? new List<string>();
            formatinstance.Options = string.Join(",",
                optionsList.Where(static s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct());

            // Optional extras (only if you use them)
            formatinstance.Validation = input.KeyfactorValidation ?? "";
            formatinstance.Message = input.KeyfactorMessage ?? "";
            formatinstance.DefaultValue = input.KeyfactorDefaultValue ?? "";
            formatinstance.Enrollment = EnrollmentMatcher(input.KeyfactorEnrollment);

            formattedlist.Add(formatinstance);
        }

        return formattedlist;

        static bool SafeToBool(string s, bool defVal)
        {
            if (string.IsNullOrWhiteSpace(s)) return defVal;
            if (bool.TryParse(s, out var b)) return b;
            s = s.Trim().ToLowerInvariant();
            return s is "1" or "y" or "yes" or "true" ? true :
                s is "0" or "n" or "no" or "false" ? false : defVal;
        }

        static int EnrollmentMatcher(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim().ToLowerInvariant();
            return s switch { "required" => 1, "hidden" => 2, "1" => 1, "2" => 2, _ => 0 };
        }
    }
    private static int? ReadMaxItemsLimit()
    {
        var s = ConfigurationManager.AppSettings["KeyfactorSearchLimit"]
                ?? ConfigurationManager.AppSettings["KeyfactorCertSearchReturnLimit"];

        if (int.TryParse(s, out var v) && v > 0) return v;   // enforce only if > 0
        return null;                                         // unlimited
    }

    public static JObject Flatten(JObject jObject, string parentName = "")
    {
        var result = new JObject();
        foreach (var property in jObject.Properties())
        {
            var propName = string.IsNullOrEmpty(parentName) ? property.Name : $"{parentName}.{property.Name}";
            if (property.Value is JObject nestedObject)
                result.Merge(Flatten(nestedObject, propName));
            else
                result[propName] = property.Value;
        }

        return result;
    }
}