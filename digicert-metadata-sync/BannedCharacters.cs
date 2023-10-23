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

using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace DigicertMetadataSync;

internal partial class DigicertSync
{
    public static List<CharDBItem> BannedCharacterParse(string input)
    {
        string pattern = "[a-zA-Z0-9-_]";

        List<CharDBItem> bannedChars = new List<CharDBItem>();

        foreach (char c in input)
        {
            if (!Regex.IsMatch(c.ToString(), pattern))
            {
                CharDBItem localitem = new CharDBItem();
                localitem.character = c.ToString();
                localitem.replacementcharacter = "null";
                bannedChars.Add(localitem);
            }
        }

        if (bannedChars.Count > 0)
        {
            Console.WriteLine("The field name " + input + " contains the following invalid characters: " +
                              string.Join("", bannedChars.Select(item => item.character)));
        }
        else
        {
            Console.WriteLine("The field name " + input + " is valid.");
        }

        return bannedChars;
    }

    public static void CheckForChars(List<ReadInMetadataField> input, List<CharDBItem> allBannedChars, bool restartandconfigrequired)
    {
        foreach (var dgfield in input)
        {
            List<CharDBItem> newChars = BannedCharacterParse(dgfield.DigicertFieldName);
            foreach (var newchar in newChars)
            {
                bool exists = allBannedChars.Any(allcharchar => allcharchar.character == newchar.character);
                if (!exists)
                {
                    allBannedChars.Add(newchar);
                    restartandconfigrequired = true;
                }
            }
        }
    }
}
