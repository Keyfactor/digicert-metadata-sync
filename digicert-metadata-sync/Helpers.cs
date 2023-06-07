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

using System.Reflection;
using System.Text.RegularExpressions;

namespace DigicertMetadataSync;

partial class DigicertSync
{
    public static int TypeMatcher(string digicerttype)
    {
        if (digicerttype.Contains("int") || digicerttype.Contains("Int"))
        {
            // 2 matches the keyfactor int type metadata field
            return 2;
        }
        else
        {
            //1 matches the keyfactor string type
            return 1;
        }
    }
}

partial class DigicertSync
{
    public static Dictionary<string, object> ClassConverter(object obj)
    {
        if (obj != null && obj != "")
        {
            Dictionary<string, object> resultdict = new Dictionary<string, object>();
            var propertylist = obj.GetType().GetProperties();

            foreach (PropertyInfo prop in propertylist)
            {
                string propName = prop.Name;
                var val = obj.GetType().GetProperty(propName).GetValue(obj, null);
                if (val != null)
                {
                    resultdict.Add(propName, val);
                }
                else
                {
                    resultdict.Add(propName, "");
                }
            }

            return resultdict;
        }
        else
        {
            return null;
        }
    }

    public static string ReplaceAllWhiteSpaces(string str, string replacement)
    {
        return Regex.Replace(str, @"\s+", "_-_");
    }

    public static bool CheckMode(string mode)
    {
        if ((mode == "kftodc") || (mode == "dctokf")){
            return true;
        }
        return false;
    }

    private static List<KeyfactorMetadataInstanceSendoff> convertlisttokf(List<ReadInMetadataField> inputlist,
        string replacementcharacter)
    {
        List<KeyfactorMetadataInstanceSendoff> formattedlist = new List<KeyfactorMetadataInstanceSendoff>();
        if (inputlist.Count != 0)
        {
            foreach (ReadInMetadataField input in inputlist)
            {
                KeyfactorMetadataInstanceSendoff formatinstance = new KeyfactorMetadataInstanceSendoff();
                if (input.KeyfactorMetadataFieldName == null || input.KeyfactorMetadataFieldName == "")
                {
                    //If name is emtpy, use autocomplete.
                    formatinstance.Name = ReplaceAllWhiteSpaces(input.DigicertFieldName, replacementcharacter);
                }
                else
                {
                    //Use user input preferred name.
                    formatinstance.Name = input.KeyfactorMetadataFieldName;
                }

                formatinstance.AllowAPI = Convert.ToBoolean(input.KeyfactorAllowAPI);
                formatinstance.Hint = input.KeyfactorHint;
                formatinstance.DataType = TypeMatcher(input.KeyfactorDataType);
                formatinstance.Description = input.KeyfactorDescription;
                formattedlist.Add(formatinstance);
            }
        }

        return formattedlist;
    }

    private static Dictionary<string, object> recursiveopener(Dictionary<string, object> dictin, List<string> keys,
        int limit)
    {
        // Recursion steps
        // Each iteration: access dict at keys[0], pop back off keys. K
        object result = new object();
        if (dictin == null)
        {
            return null;
        }

        if (limit != 0)
        {
            var location = keys[0];
            keys.RemoveAt(0);
            --limit;
            if (limit == 0)
            {
                //At the bottom of keys
                Dictionary<string, object> newdict = new Dictionary<string, object>();
                newdict[location] = dictin[location];
                return newdict;
            }
            else
            {
                //Keep recursion

                Dictionary<string, object> newdict = ClassConverter(dictin[location]);
                return recursiveopener(newdict, keys, limit);
            }
        }
        else
        {
            //Found nothing.
            return null;
        }
    }
}