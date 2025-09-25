// Copyright 2024 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
namespace DigicertMetadataSync.Models;

public class Config
{
    public string digicertApiKey { get; set; } = "";
    public string keyfactorDomainAndUser { get; set; } = "";
    public string keyfactorPassword { get; set; } = "";
    public string keyfactorAPIUrl { get; set; } = "";
    public string keyfactorDigicertIssuedCertQueryTerm { get; set; } = "DigiCert";
    public bool importAllCustomDigicertFields { get; set; } = false;
    public bool importDataForDeactivatedDigiCertFields { get; set; } = false;
    public bool syncRevokedAndExpiredCerts { get; set; } = false;
    public int keyfactorPageSize { get; set; } = 100;
    public string keyfactorDateFormat { get; set; } 
    public bool createMissingFieldsInDigicert { get; set; } = false;
}

public enum ConfigMode
{
    KFtoDC, // Keyfactor to Sectigo
    DCtoKF // Sectigo to Keyfactor
}
