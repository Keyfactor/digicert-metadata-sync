

## Overview
This tool primarily sets up metadata fields in Keyfactor for the custom metadata fields in DigiCert, which are named as such, but can also setup metadata fields in Keyfactor for non-custom fields available in DigiCert and unavailable in Keyfactor by default,   such as the Digicert Cert ID and the Organization contact.  These fields are referred to as manual fields in the context of this tool. After setting up these fields, the tool proceeds to update the contents of these fields. This tool only adds metadata to certificates that have already been imported into Keyfactor. Additionally, this tool requires a properly installed and functioning AnyGateway configured to work with Keyfactor and Digicert. The latest update allows for syncronization of custom field contents from Keyfactor to DigiCert. New fields are created in Keyfactor and DigiCert to accomodate for this.

## Installation and Usage
The tool comes as a Windows executable. The tool performs synchronization each time its run. For the tool to run automatically, it needs to be added as a scheduled process using Windows. The advised interval for running it is once per week. The files DigicertMetadataSync.dll.config and manualfields.json need to be present in the same directory as the tool for it to run correctly. The specific location from which the tool is ran does not matter, but it needs to have access to both the Keyfactor API endpoint as well as  Digicert, and appropriate permissions for access to the configuration files. 
An explanation for the settings found in these files is given below. 

## Command Line Arguments
One of these two arguments needs to be used for the tool to run.
- <b>"kftodc"</b>  
Syncronizes the contents of custom fields listed in manualfields.json from Keyfactor to DigiCert. If the fields in manualfields.json do not exist in Keyfactor or DigiCert, they are created first. Example: ```.\DigicertMetadataSync.exe kftodc```
- <b>"dctokf"</b>  
Syncronizes the contents of both custom and non-custom fields from DigiCert to Keyfactor. The fields are listed in manualfields.json, and are created if necessary.
Example: ```.\DigicertMetadataSync.exe dctokf```

## Settings
The settings currently present in these files are shown as an example and need to be configured for your specific situation.
### DigicertMetadataSync.dll.config settings
- <b>DigicertAPIKey</b>  
Standard DigiCert API access key.
- <b>DigicertAPIKeyTopPerm</b>  
DigiCert API access key with restrictions set to "None" - <b>required for sync from Keyfactor to DigiCert</b>. 
- <b>KeyfactorDomainAndUser</b>  
Same credential as used when logging into Keyfactor Command. A different set of credentials can be used provided they have adequate access permissions.
- <b>KeyfactorPassword</b>  
Password for the account used in the KeyfactorDomainAndUser field.
- <b>KeyfactorCertSearchReturnLimit</b>  
This specifies the number of certs the tool will expect to receive from Keyfactor Command. Can be set to an arbitrarily large number for unlimited or to a smaller number for testing.
- <b>KeyfactorAPIEndpoint</b>  
This should include the Keyfactor API endpoint, of the format https://domain.com/keyfactorapi/
- <b>KeyfactorDigicertIssuedCertQueryTerm</b>  
This should include the common prefix all DigiCert certs have in your Keyfactor instance. For example, "DigiCert"
- <b>ImportAllCustomDigicertFields</b>  
This setting enables the tool to import all of the custom metadata fields included in DigiCert and sync all of their data.

During the first run, the tool will scan the custom fields it will be importing for characters that are not supported in Keyfactor Metadata field names.
Each unsupported character will be shown in a file named "replacechar.json" and its replacement can be selected. If the values in the file are not populated, the tool will not run a second time.
- <b>ImportDataForDeactivatedDigiCertFields</b>  
If this is enabled, custom metadata fields that were deactivated in DigiCert will also be synced, and the data stored in these fields in certificates will be too.

### replacechar.json settings
This file is populated during the first run of the tool if the ImportAllCustomDigicertFields setting is toggled. 
The only text that needs replacing is shown as "null", and can be filled with any alphanumeric string. The "_" and "-" characters are also supported.


### manualfields.json settings
This file is used to specify which metadata fields should be synced up.

The "ManualFields" section is used to specify the non custom fields to import into Keyfactor. 

The "CustomFields" section is used to specify which of the custom metadata fields in DigiCert should be imported into Keyfactor.

- <b>DigicertFieldName</b>  
For "ManualFields", this should specify the location and name of the field in the json returned from the DigiCert API following a certificate order query. If the field is not at the top level, the input should be delimited using a "." character: "organization_contact.email".  The structure of the json the API returns can be viewed here: https://dev.digicert.com/services-api/orders/order-info/  
For "CustomFields", this should be the label of the custom metadata field as listed in DigiCert.

- <b>KeyfactorMetadataFieldName</b>  
This is the string that will be used as the field name in Keyfactor.  
For "ManualFields", this needs to be configured.  
For "CustomFields", if left blank, will use the same name as the same string as the DigicertFieldName, provided it has no spaces. 

- <b>KeyfactorDescription</b>  
This is the string that will be setup as the field description in Keyfactor.

- <b>KeyfactorDataType</b>  
The datatype the field will use in Keyfactor. Currently accepted types are Int and String.

- <b>KeyfactorDataType</b>  
String to be input into Keyfactor as the metadata field hint.

- <b>KeyfactorAllowAPI</b>  
Allows API management of this metadata field in Keyfactor. Should be set to true for continuous synchronization with this tool.

### Logging
Logging functionality can be configured via entering either "Debug" or "Trace" into the value of `<variable name="minLogLevel" value="Debug" />` in NLog.config.
