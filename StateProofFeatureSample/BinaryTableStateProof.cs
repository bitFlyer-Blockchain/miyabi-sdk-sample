using System;
using System.Threading.Tasks;
using Miyabi;
using Miyabi.Binary.Client;
using Miyabi.Binary.Models;
using Miyabi.ClientSdk.Client;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using Miyabi.Common.Proof;
using Miyabi.Common.Serialization.Json;
using Miyabi.ModelSdk.Models;
using Miyabi.ModelSdk.Models.Tables;
using NUnit.Framework;
using Utility;

namespace StateProofFeatureSample;

public class BinaryTableStateProof
{
    const string TableName = "BinaryTableWithStateProofSample";

    public static ByteString Key = ByteString.Encode("key");
    public static ByteString Value = ByteString.Encode("value");
    public static PublicKeyAddress UserAddress =
        new(Utils.GetUser0KeyPair().PublicKey);

    /// <summary>
    /// Create the table.
    /// Add data to the table.
    /// </summary>
    internal static async Task CreateBinaryTableAndData(IClient client)
    {
        // General API has SendTransactionAsync
        var generalApi = new GeneralApi(client);

        // Create table tx entry
        var createTableEntry = new CreateBinaryTable(
                TableName,
                false,
                true,
                [
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                ]);

        // Add binary value tx entry
        var addBinaryValueEntry = new BinaryAddValue(
            TableName,
            Key,
            Value,
            [UserAddress]);

        // Bundle the tx entries into single tx, sign the tx and send
        var txSigned = TransactionCreator.CreateTransactionBuilder(
                [createTableEntry, addBinaryValueEntry],
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
        GetBinaryStateProof(IClient client, ByteString key)
    {
        // Use Binary client to get the Binary state proof
        var binaryClient = new BinaryClient(client);
        var apiResult =
            await binaryClient.GetBinaryStateProofAsync(TableName, key);
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
    internal static async Task<PermissionedData<ByteString>>
        GetBinaryData(IClient client, ByteString key)
    {
        // Use Binary client to get the Binary data
        var binaryClient = new BinaryClient(client);

        // Get binary data value
        var apiResult =
            await binaryClient.GetBinaryValue(TableName, key);
        var binaryDataValue = apiResult.Value;

        // Get binary data owners
        var result =
            await binaryClient.GetBinaryOwnersAsync(TableName, key);
        var binaryDataOwners = result.Value.RowOwners;

        // Prepare the binary data object
        var binaryData =
            new PermissionedData<ByteString>(binaryDataOwners, binaryDataValue);

        Console.WriteLine($"Binary Data = {Json.SerializeObject(binaryData)}");

        return binaryData;
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
    internal static async Task VerifyBinaryStateProof(
        IClient client,
        int atHeight,
        StateProof stateProof,
        ByteString key,
        PermissionedData<ByteString> value)
    {
        var generalApi = new GeneralApi(client);

        // Get the table id
        var tableId = ModelUtils.GetTableId(TableName);

        // Get the hash of the table entry value
        var entryValueHash =
            BinaryModelUtils.GetValueHash(value);

        // Calculate the root hash using table data and validate
        var calculatedRootHash = stateProof.ComputeRootHashFromEntry(
            tableId,
            key,
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