using System;
using System.Threading.Tasks;
using Miyabi.NFT.Models;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Models;
using Miyabi.Common.Proof;
using Miyabi.Common.Serialization.Json;
using Miyabi.ModelSdk.Models;
using Miyabi.NFT.Client;
using NUnit.Framework;
using Utility;

namespace StateProofFeatureSample;

public class NftTableStateProof
{
    const string TableName = "NFTTableWithStateProofSample";

    public static string TokenId = "my_token_id";
    public static PublicKeyAddress TokenOwner =
        new(Utils.GetUser0KeyPair().PublicKey);

    /// <summary>
    /// Create the table.
    /// Add data to the table.
    /// </summary>
    internal static async Task CreateNftTableAndData(IClient client)
    {
        // General API has SendTransactionAsync
        var generalApi = new GeneralApi(client);

        // Create table tx entry
        var createTableEntry = new CreateNFTTable(
            TableName,
            false,
            true,
            [
                new PublicKeyAddress(
                    Utils.GetOwnerKeyPair().PublicKey)
            ]);

        // Add NFT value tx entry
        var addNftEntry = new NFTAdd(
            TableName,
            TokenId,
            TokenOwner);

        // Bundle the tx entries into single tx, sign the tx and send
        var txSigned = TransactionCreator.CreateTransactionBuilder(
                [createTableEntry, addNftEntry],
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
        GetNftStateProof(IClient client, string tokenId)
    {
        // Use NFT client to get the NFT state proof
        var nftClient = new NFTClient(client);
        var apiResult =
            await nftClient.GetNFTStateProofAsync(TableName, tokenId);
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
    internal static async Task<Address> GetNftData(
        IClient client,
        string tokenId)
    {
        // Use NFT client to get the NFT data
        var nftClient = new NFTClient(client);

        var apiResult =
            await nftClient.GetOwnerOfAsync(TableName, tokenId);
        var nftDataValue = apiResult.Value;

        Console.WriteLine($"NFT tokenId data = {nftDataValue}");

        return nftDataValue;
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
    internal static async Task VerifyNftStateProof(
        IClient client,
        int atHeight,
        StateProof stateProof,
        string tokenId,
        Address value)
    {
        var generalApi = new GeneralApi(client);

        // Get the table id
        var tableId = ModelUtils.GetTableId(TableName);

        // Get the actual data key for the table entry
        var rawNftKey =
            NFTKeyUtils.TokenToKey(tokenId);

        // Get the hash of the table entry value
        var entryValueHash =
            NFTModelUtils.GetValueHash(value);

        // Calculate the root hash using table data and validate
        var calculatedRootHash = stateProof.ComputeRootHashFromEntry(
            tableId,
            rawNftKey,
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
