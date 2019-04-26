# Inventor Design Automation with Vault

![Platforms](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.Net%20Framework-4.7.2-blue.svg)
[![Design Automation](https://img.shields.io/badge/Design%20Automation-v3-blue.svg)](https://forge.autodesk.com/api/design-automation-cover-page/)

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](http://opensource.org/licenses/MIT)
![Inventor](https://img.shields.io/badge/Inventor-2019-yellow.svg)
![Vault](https://img.shields.io/badge/Vault-2019-yellow.svg)

 This sample is a .NET console app that demostrates the use of Inventor Design Automation on files in Vault.
 It launches a Vault UI that allows the user to select a file in UI, checks out the file, changes an iProperty of the file using Design Automation, and checks the file back in to Vault.
 The code used for the bundle can be found in the UpdateiProp project.
 
 # Setup

## Prerequisites
1. Create a Forge account, as you will need the key and secret for running the app. Refer this [tutorial](http://learnforge.autodesk.io/#/account/) for more details.
2. Vault 2019 (for including references)
3. Visual Studio
4. knowledge of C#

## Running locally
1. Clone or download this project.
2. Add to your env. variables
    * FORGE_CLIENT_ID
    * FORGE_CLIENT_SECRET
3. Existing solution requires that the Vault licensing module - clmloader.dll be placed in the output directory. Code can be modified to use server side licensing too. 
4. Build solution and run VaultInventorDA project
5. Selected file would be updated and checked in Vault. Results can be verified using Vault Explorer.

## License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE) file for full details.

## Written by

Sajith Subramanian, [Forge Partner Development](http://forge.autodesk.com)
