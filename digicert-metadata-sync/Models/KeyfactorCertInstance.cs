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
    public class KeyfactorCert
    {
        public int Id { get; set; }
        public string Thumbprint { get; set; }
        public string SerialNumber { get; set; }
        public string IssuedDN { get; set; }
        public string IssuedCN { get; set; }
        public DateTime ImportDate { get; set; }
        public DateTime NotBefore { get; set; }
        public DateTime NotAfter { get; set; }
        public string IssuerDN { get; set; }
        public object PrincipalId { get; set; }
        public int TemplateId { get; set; }
        public int CertState { get; set; }
        public int KeySizeInBits { get; set; }
        public int KeyType { get; set; }
        public object RequesterId { get; set; }
        public object IssuedOU { get; set; }
        public object IssuedEmail { get; set; }
        public int KeyUsage { get; set; }
        public string SigningAlgorithm { get; set; }
        public string CertStateString { get; set; }
        public string KeyTypeString { get; set; }
        public object RevocationEffDate { get; set; }
        public object RevocationReason { get; set; }
        public object RevocationComment { get; set; }
        public int CertificateAuthorityId { get; set; }
        public string CertificateAuthorityName { get; set; }
        public string TemplateName { get; set; }
        public bool ArchivedKey { get; set; }
        public bool HasPrivateKey { get; set; }
        public object PrincipalName { get; set; }
        public object CertRequestId { get; set; }
        public object RequesterName { get; set; }
        public string ContentBytes { get; set; }
        public Extendedkeyusage[] ExtendedKeyUsages { get; set; }
        public Subjectaltnameelement[] SubjectAltNameElements { get; set; }
        public Crldistributionpoint[] CRLDistributionPoints { get; set; }
        public object[] LocationsCount { get; set; }
        public object[] SSLLocations { get; set; }
        public object[] Locations { get; set; }
        public Metadata Metadata { get; set; }
        public int CertificateKeyId { get; set; }
        public int CARowIndex { get; set; }
        public Detailedkeyusage DetailedKeyUsage { get; set; }
        public bool KeyRecoverable { get; set; }
    }

    public class Metadata
    {
    }

    public class Detailedkeyusage
    {
        public bool CrlSign { get; set; }
        public bool DataEncipherment { get; set; }
        public bool DecipherOnly { get; set; }
        public bool DigitalSignature { get; set; }
        public bool EncipherOnly { get; set; }
        public bool KeyAgreement { get; set; }
        public bool KeyCertSign { get; set; }
        public bool KeyEncipherment { get; set; }
        public bool NonRepudiation { get; set; }
        public string HexCode { get; set; }
    }

    public class Extendedkeyusage
    {
        public int Id { get; set; }
        public string Oid { get; set; }
        public string DisplayName { get; set; }
    }

    public class Subjectaltnameelement
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public int Type { get; set; }
        public string ValueHash { get; set; }
    }

    public class Crldistributionpoint
    {
        public int Id { get; set; }
        public string Url { get; set; }
        public string UrlHash { get; set; }
    }
}