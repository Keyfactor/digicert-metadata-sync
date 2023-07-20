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
    public class DigicertCert
    {
        public int id { get; set; }
        public DigicertCertificate certificate { get; set; }
        public string status { get; set; }
        public bool is_renewal { get; set; }
        public DateTime date_created { get; set; }
        public DigicertOrganization organization { get; set; }
        public int validity_years { get; set; }
        public bool disable_renewal_notifications { get; set; }
        public int auto_renew { get; set; }
        public int auto_reissue { get; set; }
        public DigicertContainer container { get; set; }
        public DigicertProduct product { get; set; }
        public DigicertOrganization_Contact organization_contact { get; set; }
        public DigicertTechnical_Contact technical_contact { get; set; }
        public DigicertUser user { get; set; }
        public int purchased_dns_names { get; set; }
        public DigicertRequest[] requests { get; set; }
        public bool is_out_of_contract { get; set; }
        public string payment_method { get; set; }
        public string product_name_id { get; set; }
        public DigicertCustom_Fields[] custom_fields { get; set; }
        public bool disable_issuance_email { get; set; }
        public bool is_guest_access_enabled { get; set; }
    }

    public class DigicertCertificate
    {
        public string common_name { get; set; }
        public string[] dns_names { get; set; }
        public DateTime date_created { get; set; }
        public string csr { get; set; }
        public string serial_number { get; set; }
        public DigicertCertOrganization organization { get; set; }
        public string[] organization_units { get; set; }
        public DigicertServer_Platform server_platform { get; set; }
        public string signature_hash { get; set; }
        public int key_size { get; set; }
        public DigicertCa_Cert ca_cert { get; set; }
    }

    public class DigicertCertOrganization
    {
        public int id { get; set; }
    }

    public class DigicertServer_Platform
    {
        public int id { get; set; }
        public string name { get; set; }
        public string install_url { get; set; }
        public string csr_url { get; set; }
    }

    public class DigicertCa_Cert
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class DigicertOrganization
    {
        public int id { get; set; }
        public string name { get; set; }
        public string assumed_name { get; set; }
        public string display_name { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string country { get; set; }
    }

    public class DigicertContainer
    {
        public int id { get; set; }
        public string name { get; set; }
        public bool is_active { get; set; }
    }

    public class DigicertProduct
    {
        public string name_id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string validation_type { get; set; }
        public string validation_name { get; set; }
        public string validation_description { get; set; }
        public bool csr_required { get; set; }
    }

    public class DigicertOrganization_Contact
    {
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
        public string job_title { get; set; }
        public string telephone { get; set; }
        public string telephone_extension { get; set; }
    }

    public class DigicertTechnical_Contact
    {
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
        public string job_title { get; set; }
        public string telephone { get; set; }
        public string telephone_extension { get; set; }
    }

    public class DigicertUser
    {
        public int id { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
    }

    public class DigicertRequest
    {
        public int id { get; set; }
        public DateTime date { get; set; }
        public string type { get; set; }
        public string status { get; set; }
        public string comments { get; set; }
    }

    public class DigicertCustom_Fields
    {
        public int metadata_id { get; set; }
        public string label { get; set; }
        public string value { get; set; }
    }
}