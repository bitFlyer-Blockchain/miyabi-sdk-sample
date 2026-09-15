# miyabi-sdk-sample

## Projects

| Name                      | Description                                                                                                                                          |
|:--------------------------|:-------------------------------------------------------------------------------------------------------------------------------------------------------|
| AssetSample               | Sample for asset table such as creating asset table, generating asset, moving asset, verifying asset, checking table/entry existence and dumping the table. |
| BinarySample              | Sample for binary table such as creating binary table, adding raw values, reading them back individually/in batch/in bulk, and checking table/entry existence. |
| CombinedTransactionSample | Sample for combined transaction that swaps exchange between two tables.                                         |
| EntitySample              | Sample for entity table such as creating entity table, adding value, creating parent-child relations, walking the entity tree, and checking table/entry existence. |
| GeneralApiCallSample      | Sample for showing how to call the general-purpose blockchain APIs: listing tables, node/network info, batch transaction submission, transaction/block lookup by id or height, and per-entry change history. |
| SmartContractSample       | Sample for smart contract such as deploying, instantiating, invoking and querying contracts, discovering deployed assemblies/instances, and multi-sig accounts. |
| NFTSample                 | Sample for nft table such as creating nft table, adding nft, moving nft, showing balance of nft, and checking table/entry existence.             |
| PrivateDataSample         | Sample for private data such as creating private data table, adding data, showing data (raw/hashed), checking existence, and listing entries.       |
| StateProofFeatureSample   | Sample for state proof feature such as enabling feature, getting and verifying the state proof                  |
| Utility                   | Utility methods.                                                                                                |

## Code update

In [Utility/Utils.cs](Utility/Utils.cs), there are some configuration that need to be updated according to your environment.

| Item                      | Description                                                                                                |
|:--------------------------|:-----------------------------------------------------------------------------------------------------------|
| ApiUrl                    | URL of the miyabi node.                                                                                    |
| GetTableAdminKeyPair()    | The parameter of `GetKeyPair` is table admin private key. This is used to create tables.                   |
| GetContractAdminKeyPair() | The parameter of `GetKeyPair` is contract admin private key. This is used to deploy smart contracts.       |
| GetOwnerKeyPair()         | The parameter of `GetKeyPair` is table and/or contract owner's private key. Whatever value is acceptable.  |
| GetUser0KeyPair()         | The parameter of `GetKeyPairFromKeystore` is table user's p12 keystore path.                               |
| GetUser1KeyPair()         | The parameter of `GetKeyPairFromKeystore` is table user's p12 keystore path.                               |
| PdoPublicKey              | Public key of the pdo member.                                                                              |
| PdoUrl                    | URL of the pdo member.                                                                                     |

## Prerequisites

The .NET 10 SDK is required to build and run these projects (see
[Common.props](Common.props)).

## Execution steps

1. Put provided nuget packages into a folder and update your Visual Studio nuget settings.
2. Clean the cache of nuget and restore all packages using nuget.
3. Update private keys in reference to [Code update](#code-update) section.

