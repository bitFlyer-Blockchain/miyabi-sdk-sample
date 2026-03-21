using System;
using System.Threading.Tasks;
using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Models;
using Miyabi.Common.Proof;
using Miyabi.Common.Serialization.Json;
using Miyabi.Entity.Client;
using Miyabi.Entity.Models;
using Miyabi.ModelSdk.Models;
using Miyabi.ModelSdk.Models.Tables;
using NUnit.Framework;
using Utility;

namespace StateProofFeatureSample;

public class EntityTableStateProof
{
	const string TableName = "EntityTableWithStateProofSample";

	public static ByteString Key = ByteString.Parse("10");
	public static string Value = "Value";
	public static PublicKeyAddress UserAddress =
		new(Utils.GetUser0KeyPair().PublicKey);

	/// <summary>
	/// Create the table.
	/// Add data to the table.
	/// </summary>
	internal static async Task CreateEntityTableAndData(IClient client)
	{
		// General API has SendTransactionAsync
		var generalApi = new GeneralApi(client);

		// Create table tx entry
		var createTableEntry = new CreateEntityTable(
			TableName,
			false,
			true,
			new Address[]
			{
				new PublicKeyAddress(
					Utils.GetOwnerKeyPair().PublicKey)
			});

		// Add entity value tx entry
		var addEntityEntry = new AddEntity(
			Value,
			TableName,
			Key,
			owners: new []{ UserAddress });

		// Bundle the tx entries into single tx, sign the tx and send
		var txSigned = TransactionCreator.CreateTransactionBuilder(
				new ITransactionEntry[] { createTableEntry, addEntityEntry },
				new[]
				{
					new SignatureCredential(Utils.GetTableAdminKeyPair().PublicKey),
					new SignatureCredential(Utils.GetOwnerKeyPair().PublicKey)
				})
			.Sign(new[]
			{
				Utils.GetTableAdminKeyPair().PrivateKey,
				Utils.GetOwnerKeyPair().PrivateKey
			})
			.Build();

		await generalApi.SendTransactionAsync(txSigned);

		var result = await Utils.WaitTx(generalApi, txSigned.Id);
		Console.WriteLine($"txId={txSigned.Id}, result={result}");
	}

	/// <summary>
	/// Get the state-proof for the table data.
	/// </summary>
	internal static async Task<(int, StateProof)>
		GetEntityStateProof(IClient client, ByteString key)
	{
		// Use Entity client to get the Entity state-proof
		var entityClient = new EntityClient(client);
		var apiResult =
			await entityClient.GetEntityStateProofAsync(TableName, key);
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
		GetEntityData(IClient client, ByteString key)
	{
		// Use entity client to get the entity data
		var entityClient = new EntityClient(client);

		// Get entity data value
		var apiResult =
			await entityClient.GetEntityAsync(TableName, key);
		var entityDataValue = apiResult.Value.HexData;

		// Get entity data owners
		var result =
			await entityClient.GetEntityOwnersAsync(TableName, key);
		var entityDataOwners = result.Value.RowOwners;

		// Prepare the entity data object
		var entityData =
			new PermissionedData<ByteString>(entityDataOwners, entityDataValue);

		Console.WriteLine($"Entity Data = {Json.SerializeObject(entityData)}");

		return entityData;
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
	internal static async Task VerifyEntityStateProof(
		IClient client,
		int atHeight,
		StateProof stateProof,
		ByteString key,
		PermissionedData<ByteString> value)
	{
		var generalApi = new GeneralApi(client);
		
		// Get the table id
		var tableId = ModelUtils.GetTableId(TableName);

		// Get the actual data key for the table entry
		var rawEntityKey = EntityKeyUtils.GetDataKey(key);

		// Get the hash of the table entry value
		var entryValueHash =
			EntityModelUtils.GetValueHash(value);

		// Calculate the root hash using table data and validate
		var calculatedRootHash = stateProof.ComputeRootHashFromEntry(
			tableId,
			rawEntityKey,
			entryValueHash);
		Assert.AreEqual(calculatedRootHash, stateProof.ProofRootHash);

		Utils.GetConsoleBanner();
		Console.WriteLine("(1/2) Verify success: Calculated and " +
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