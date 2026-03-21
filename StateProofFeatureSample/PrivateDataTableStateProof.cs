using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Models;
using Miyabi.Common.Proof;
using Miyabi.Common.Serialization.Json;
using Miyabi.Cryptography;
using Miyabi.ModelSdk.Models;
using Miyabi.ModelSdk.Models.Tables;
using Miyabi.PrivateData.Client;
using Miyabi.PrivateData.Models;
using Miyabi.PrivateData.Models.Payloads;
using Miyabi.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;
using Utility;

namespace StateProofFeatureSample;

public class PrivateDataTableStateProof
{
	const string RawTableName = "PrivateDataTableWithStateProofSample";

	public static ByteString RawKey = ByteString.Parse("01");
	public static ByteString RawValue = ByteString.Parse("02");
	public static Address RowOwnerAddress =
		new PublicKeyAddress(Utils.GetUser0KeyPair().PublicKey);

	/// <summary>
	/// Create the table.
	/// Add data to the table.
	/// </summary>
	internal static async Task CreatePrivateDataTableAndData(IClient client)
	{
		var pdoMembers = Utils.PdoMembers;
		var hashedTableName = PrivateDataModelUtils.ComputeHash(RawTableName);

		// Create table tx entry
		var createTableEntry = new CreatePrivateDataTable(
			// Need to pass hashed data
			hashedTableName,
			false,
			true,
			new Address[]
			{
				new PublicKeyAddress(
					Utils.GetOwnerKeyPair().PublicKey)
			},
			false,
			PermissionModel.TableOrRow,
			pdoMembers.Select(x => Address.Decode(x.Admin)).ToList());

		// Create the corresponding raw data payload entry
		// These data finally register into PDO members
		// It means only PDO members can return these raw data
		var createTablePayloadEntry = new CreatePrivateDataTablePayload(RawTableName);

		// Add privateData value tx entry
		var hashedHexKey = PrivateDataModelUtils.ComputeHash(RawKey);
		var hashedHexValue = PrivateDataModelUtils.ComputeHash(RawValue);

		var addPrivateDataEntry = new AddPrivateData(
			hashedTableName,
			hashedHexKey,
			hashedHexValue,
			new[] { RowOwnerAddress },
			pdoMembers.Select(x => Address.Decode(x.Admin)));

		// Create the corresponding raw data payload entry
		var addPrivateDataPayloadEntry =
			new AddPrivateDataPayload(RawTableName, RawKey, RawValue);

		// It must have pdo members and table admin to create a table
		var requiredCredentials =
			pdoMembers.Select(x => Credential.Decode(x.Admin))
				.Append(TransactionCreator.AsCredential(Utils.GetTableAdminKeyPair().PrivateKey))
				.Append(TransactionCreator.AsCredential(Utils.GetOwnerKeyPair().PrivateKey));

		// Bundle the tx entries into a single tx
		var unsignedTx =
			TransactionCreator.CreateTransaction(
				new ITransactionEntry[] { createTableEntry, addPrivateDataEntry },
				requiredCredentials);

		// Bundle the payloads and corresponding tx into a private tx
		var privateTx = new PrivateTransaction(
			MessageConverter.Serialize(unsignedTx),
			new List<ByteString>
			{
				MessageConverter.Serialize(createTablePayloadEntry),
				MessageConverter.Serialize(addPrivateDataPayloadEntry),
			});

		// Send the private tx to the PDO members.
		// PDO members will validate payload, sign the miyabi tx
		// and send back the evidence. Create and send a signed tx
		// using the PDO evidences.
		var (txId, txResult) =
			await SendAndWaitPrivateTx(
				client,
				privateTx,
				new[]
				{
					Utils.GetTableAdminKeyPair().PrivateKey,
					Utils.GetOwnerKeyPair().PrivateKey,
				});

		Console.WriteLine($"{nameof(CreatePrivateDataTableAndData)} result:");
		Console.WriteLine(JsonConvert.SerializeObject(new
		{
			TxId = txId,
			Result = txResult,
			TableName = RawTableName,
			HashedTableName = hashedTableName,
			Key = RawKey,
			HashedHexKey = hashedHexKey,
			Value = RawValue,
			HashedHexValue = hashedHexValue
		}, Formatting.Indented));
	}

	/// <summary>
	/// Get the state-proof for the table data.
	/// </summary>
	internal static async Task<(int, StateProof)>
		GetPrivateDataStateProof(IClient client, ByteString rawKey)
	{
		// Use PrivateData client to get the PrivateData state proof
		var privateDataClient =
			new PrivateDataClient(
				client,
				Utils.GetBypassRemoteCertificateValidationHandler());
		var apiResult =
			await privateDataClient.GetPrivateDataStateProofAsync(
				RawTableName,
				rawKey);
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
		GetPrivateDataEntryData(IClient client, ByteString key)
	{
		// Use PrivateData client to get the PrivateData data
		var privateDataClient = new PrivateDataClient(
			client,
			Utils.GetBypassRemoteCertificateValidationHandler());

		// Get privateData data value
		var apiResult = await privateDataClient.GetPrivateDataEntryAsync(
			RawTableName,
			key,
			false);

		var privateDataEntryValue = apiResult.Value;

		// Get privateData data owners
		var result = await privateDataClient.GetPrivateDataOwnersAsync(
			RawTableName,
			key,
			false);
		var privateDataEntryOwners = result.Value.RowOwners;

		// Prepare the privateData data object.
		var privateDataEntryData = new PermissionedData<ByteString>(
			privateDataEntryOwners,
			privateDataEntryValue.Value);

		Console.WriteLine(
			$"PrivateData Entry's data = {Json.SerializeObject(privateDataEntryData)}");

		return privateDataEntryData;
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
	internal static async Task VerifyPrivateDataStateProof(
		IClient client,
		int atHeight,
		StateProof stateProof,
		ByteString key,
		PermissionedData<ByteString> value)
	{
		var generalApi = new GeneralApi(client);

		// Get the table id. Need to use the hashed table name.
		var tableId = ModelUtils.GetTableId(
			PrivateDataModelUtils.ComputeHash(RawTableName));

		// Raw key is not stored in the blockchain directly.
		// Need to use the hashed key.
		var hashedKey = PrivateDataModelUtils.ComputeHash(key);

		// Get the hash of the table entry value
		var entryValueHash =
			PrivateDataModelUtils.GetValueHash(value);

		// Calculate the root hash using table data and validate
		var calculatedRootHash = stateProof.ComputeRootHashFromEntry(
			tableId,
			hashedKey,
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

	/// <summary>
	/// Send the private tx to all PDOs.
	/// Gather the evidences and create a signed tx.
	/// Validate the tx.
	/// Send the signed tx.
	/// Wait for the tx till it gets processed.
	/// </summary>
	static async Task<(ByteString txId, string txResult)>
		SendAndWaitPrivateTx(
			IClient client,
			PrivateTransaction privateTx,
			IEnumerable<PrivateKey> signingPrivateKeys)
	{
		// Obtain evidences for a private tx signed by all PDOs
		var evidences = await new PrivateDataClient(
				client,
				Utils.GetBypassRemoteCertificateValidationHandler())
			.SignPrivateTransactionAsync(privateTx);

		// Create a final transaction using PDO members' evidences signed by
		// other table admins(s)
		var signedTx = new TransactionBuilder(
				MessageConverter.Deserialize<Transaction>(
					privateTx.Transaction))
			.AddEvidence(evidences.Value)
			.Sign(signingPrivateKeys)
			.Build();

		if (!signedTx.ValidateCredentials())
		{
			await Console.Error.WriteLineAsync(
				"Failed to validate final signed" +
				$" transaction with tx id: {signedTx.Id}");
		}

		// Send transaction to general api endpoint
		var generalApi = new GeneralApi(client);
		await generalApi.SendTransactionAsync(signedTx);

		// Wait until the transaction is stored in a block and get the result
		var result = await Utils.WaitTx(generalApi, signedTx.Id);
		return (signedTx.Id, result);
	}
}