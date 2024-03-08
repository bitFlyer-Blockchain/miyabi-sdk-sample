# miyabi-sdk-sample

## Projects

| Name                      | Description                                                                                                     |
|:--------------------------|:----------------------------------------------------------------------------------------------------------------|
| AssetSample               | Sample for asset table such as creating asset table, generating asset, moving asset and verifying asset.        |
| CombinedTransactionSample | Sample for combined transaction that swaps exchange between two tables.                                         |
| EntitySample              | Sample for entity table such as creating entity table, adding value, creating parent-child relations and so on. |
| GeneralApiCallSample      | Sample for showing how to call APIs.                                                                            |
| SmartContractSample       | Sample for smart contract such as deploying, instantiating, invoking and querying contracts and so on.          |
| NFTSample                 | Sample for nft table such as creating nft table, adding nft, moving nft and showing balance of nft.             |
| PrivateDataSample         | Sample for private data such as creating private data table, adding data and showing data.                      |
| Utility                   | Utility methods.                                                                                                |

## Code update

In [Utility/Utils.cs](Utility/Utils.cs), there are some configuration that need to be updated according to your environment.

| Item                      | Description                                                                                                |
|:--------------------------|:-----------------------------------------------------------------------------------------------------------|
| ApiUrl                    | URL of the miyabi node.                                                                                    |
| GetTableAdminKeyPair()    | The parameter of `GetKeyPair` is table admin private key. This is used to create tables.                   |
| GetContractAdminKeyPair() | The parameter of `GetKeyPair` is contract admin private key. This is used to deploy smart contracts.       |
| GetOwnerKeyPair()         | The parameter of `GetKeyPair` is table and/or contract owner's private key. Whatever value is acceptable.  |
| GetUser0KeyPair()         | The parameter of `GetKeyPair` is table user's private key. Whatever value is acceptable.                   |
| GetUser1KeyPair()         | The parameter of `GetKeyPair` is table user's private key. Whatever value is acceptable.                   |
| PdoPublicKey              | Public key of the pdo member.                                                                              |
| PdoUrl                    | URL of the pdo member.                                                                                     |

## Execution steps

1. Put provided nuget packages into a folder and update your Visual Studio nuget settings.
2. Clean the cache of nuget and restore all packages using nuget.
3. Update private keys in reference to [Code update](#code-update) section.

