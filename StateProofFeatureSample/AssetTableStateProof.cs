using System;
using System.Threading.Tasks;
using Miyabi.Asset.Client;
using Miyabi.Asset.Models;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Models;
using Miyabi.Common.Proof;
using Miyabi.Common.Serialization.Json;
using Miyabi.ModelSdk.Models;
using NUnit.Framework;
using Utility;

namespace StateProofFeatureSample;

internal class AssetTableStateProof
{
    const string TableName = "AssetTableWithStateProofSample";

    public static PublicKeyAddress UserAddress =
        new(Utils.GetUser0KeyPair().PublicKey);
    public static decimal UserBalance = 1000.0M;

    /// <summary>
    /// Create the table.
    /// Add data to the table.
    /// </summary>
    internal static async Task CreateAssetTableAndData(IClient client)
    {
        // General API has SendTransactionAsync
        var generalApi = new GeneralApi(client);

        // Create table tx entry
        var createTableEntry = new CreateAssetTable(
            TableName,
            false,
            true,
            [
                new PublicKeyAddress(
                    Utils.GetOwnerKeyPair().PublicKey)
            ]);

        // Asset generate tx entry
        var generateAssetEntry = new AssetGen(
            TableName,
            UserBalance,
            UserAddress);

        // Bundle the tx entries into single tx, sign the tx and send
        var txSigned = TransactionCreator.CreateTransactionBuilder(
                [createTableEntry, generateAssetEntry],
                [
                    new SignatureCredential(Utils.GetTableAdminKeyPair().PublicKey),
                    new SignatureCredential(Utils.GetOwnerKeyPair().PublicKey)
                ])
            .Sign([
                Utils.GetTableAdminKeyPair().PrivateKey,
                Utils.GetOwnerKeyPair().PrivateKey
            ])
            .Build();

        await generalApi.SendTransactionAsync(txSigned);

        var result = await Utils.WaitTx(generalApi, txSigned.Id);
        Console.WriteLine($"txId={txSigned.Id}, result={result}");
    }

    /// <summary>
    /// Get the state-proof for the table data.
    /// </summary>
    internal static async Task<(int, StateProof)>
        GetAssetStateProof(IClient client, Address address)
    {
        // Use Asset client to get the Asset data state-proof
        var assetClient = new AssetClient(client);
        var apiResult =
            await assetClient.GetAssetStateProofAsync(TableName, address);
        (int atHeight, var stateProof) =
            (apiResult.AtHeight, apiResult.Value);

        Console.WriteLine(
            $"atHeight={atHeight}, stateProof=" +
            $"{Json.SerializeObject(stateProof)}");

        return (atHeight, stateProof);
    }

    /// <summary>
    /// Get the table data from the blockchain.
    /// </summary>
    internal static async Task<decimal>
        GetAssetData(IClient client, Address address)
    {
        // Use Asset client to get the Asset data
        var assetClient = new AssetClient(client);

        var apiResult =
            await assetClient.GetAssetAsync(TableName, address);
        var assetDataValue = apiResult.Value;

        Console.WriteLine(
            $"Asset Data = {Json.SerializeObject(assetDataValue)}");

        return assetDataValue;
    }

    /// <summary>
    /// Verify the state proof.
    /// </summary>
    /// <remarks>
    /// `a`:= State proof root hash obtained from the State proof object,
    /// `b`:= Calculated root hash using actual data & the State proof object,
    /// `c`:= State-hash value obtained from `BlockHeader` using blockchain api,
    /// Verification requires `a`==`b`==`c`
    /// </remarks>
    internal static async Task VerifyAssetStateProof(IClient client,
        int atHeight,
        StateProof stateProof,
        Address address,
        decimal balance)
    {
        var generalApi = new GeneralApi(client);

        // Get the table id
        var tableId = ModelUtils.GetTableId(TableName);

        // Get the hash of the table entry value
        var entryValueHash =
            AssetModelUtils.GetValueHash(balance);

        // Calculate the root hash using table data and validate
        var calculatedRootHash = stateProof.ComputeRootHashFromEntry(
            tableId,
            address.Encoded,
            entryValueHash);

        Assert.AreEqual(calculatedRootHash, stateProof.ProofRootHash);
        Utils.GetConsoleBanner();
        Console.WriteLine(
            "(1/2) Verify success: Calculated and " +
            "expected state proof root hash matched.");
        Console.WriteLine(
            $"calculatedRootHash={calculatedRootHash}, " +
            $"stateProof.ProofRootHash={stateProof.ProofRootHash}");
        Utils.GetConsoleBanner();

        // Get the state hash from the block header and match
        var headerInfo = (await generalApi
            .GetHeaderAsync(atHeight)).Value;
        Assert.AreEqual(
            headerInfo.Header.StateHash,
            stateProof.ProofRootHash);

        Console.WriteLine(
            "(2/2) Verify success: State Hash from Block header" +
            " and expected state proof root hash matched.");
        Console.WriteLine(
            $"BlockHeader.StateHash(h={atHeight})=" +
            $"{headerInfo.Header.StateHash}, stateProof.ProofRootHash=" +
            $"{stateProof.ProofRootHash}");
        Utils.GetConsoleBanner();
    }
}