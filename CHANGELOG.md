Version 3.0.0
    
    ⚠️ Important Notice
    **Configuration files and their location have changed since version 2.1.0** Please review the documentation and see the new stock configuration files for guidance on how to set up the tool. 
    The configuration files will need to be placed in the `config` subdirectory for use with the tool.
    
    Rewrote the sync engine to improve performance and resilience.
    New retry logic now automatically backs off when rate limits are hit on DigiCert.
    Config system now uses json file instead of xml, and all config files are aggregated in the config directory.
    Fixed issue with new Keyfactor versions being broken due to lack of DisplayOrder in the metadata fields API.
    Fixed issue with email fields not syncing properly.
    Implemented a new logging system using NLog, with log files stored in the logs directory, and an nlog.config file.

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