Version 2.2.0
    Fixed a bug that prevented tool from correctly recognizing command line args.
    Added Sync Reissue to the config file options.
    Improved documentation.
    Adjust manualfields.json to work with modern versions of Keyfactor.

Version 2.1.0

    Added a system that gathers all non-Keyfactor friendly characters and allows the user to configure an alternative.
    Added pagination based batch processing, memory consumption has been drastically reduced.

Version 2.0.3  

    Added a setting to enable or disable syncing deactivated custom fields from DigiCert.

Version 2.0.2

    Fixed issue with additional_emails field not syncing.
    Added independent logging via NLog.

Version 2.0.1

    Fixed issue with no input for either custom or manual fields leading to a crash.
    Fixed issue with data for imported DigiCert fields renamed with a replacement character not syncing back to DigiCert.
    Fixed possible crash caused by importing DigiCert custom fields with "Anything" data type.

Version 2.0.0

    Added ability to sync custom fields from Keyfactor to DigiCert.
    Tool now requires command line argument to specify sync direction: "dctokf" for DigiCert to Keyfactor and "kftodc" for Keyfactor to DigiCert.
    New DigiCert API Key with restrictions set to "None" in DigiCert config required to perform sync from Keyfactor to Digicert.

Version 1.0

    Initial Release