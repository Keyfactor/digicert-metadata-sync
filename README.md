# Digicert Metadata Sync

This project is meant to be a template to quickly build a basic integration product build. Currently in dev, a work in progress,

#### Integration status: Prototype - Demonstration quality. Not for use in customer environments.

## About the Keyfactor 







# digicert-metadata-sync
A tool to automatically synchronize metadata fields and their content between DigiCert and Keyfactor.

## Overview
This tool primarily sets up metadata fields in Keyfactor for both the custom metadata fields in DigiCert, which are named as such, but can also setup metadata fields in Keyfactor for non-custom fields available in DigiCert and unavailable in Keyfactor by default,   such as the Digicert Cert ID and the Organization contact. These fields are referred to as manual fields in the context of this tool.

Prior to use, this tool needs to be configured through the following files: app.config and manualfields.json.

## Settings

### app.config settings
- <b>DigicertAPIKey</b>  
Standard DigiCert API access key 
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
- <b>ReplaceDigicertWhiteSpaceCharacterInName</b>  
In case the ImportAllCustomDigicertFields setting is used, this is necessary to for metadata field label conversion. DigiCert supports spaces in labels and Keyfactor does not, so this replaces the spaces in the name with your character sequence of choice.

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

