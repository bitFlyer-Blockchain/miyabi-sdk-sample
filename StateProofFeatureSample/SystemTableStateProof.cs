using System;
using System.Threading.Tasks;
using Miyabi;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Proof;
using Miyabi.Common.Serialization.Json;
using Miyabi.ModelSdk.Models;
using NUnit.Framework;
using Utility;

namespace StateProofFeatureSample;

/// <summary>
/// System data tables are internal tables of the miyabi blockchain.
/// These tables do not store any blockchain user data.
/// And thus, the state proof for the table data is usually not needed.
/// The table keys and table data are normally unavailable to normal users.
/// But, there are few tables, for which getting the table data is supported.
/// One such table is used in this example to showcase the state proof feature.
/// </summary>
public class SystemTableStateProof
{
	const string SystemTableName = "SYSTEM";
	public static ByteString StateHeightKey = ByteString.Encode("@hgt");

	/// <summary>
	/// Get the state-proof for the table data.
	/// </summary>
	internal static async Task<(int, StateProof)> GetSystemTableStateProof(
		IClient client)
	{
		// Use general api to get the state proof.
		var generalApi = new GeneralApi(client);
		var apiResult =
			await generalApi.GetStateProofAsync(
				SystemTableName,
				StateHeightKey);

		(int atHeight, var stateProof) =
			(apiResult.AtHeight, apiResult.Value);

		Console.WriteLine(
			$"atHeight={atHeight}, stateProof=" +
			$"{Json.SerializeObject(stateProof)}");

		return (atHeight, stateProof);
	}

	/// <summary>
	/// Gets the current height.
	/// This data is stored in the System table.
	/// </summary>
	internal static async Task<int>
		GetCurrentHeight(IClient client)
	{
		// Use general api to get the height
		var generalApi = new GeneralApi(client);

		// Get the blockchain height(stored in the system table)
		var apiResult =
			await generalApi.GetHeightAsync();
		var heightData = apiResult.Value;

		Console.WriteLine(
			$"Current height = {heightData}");

		return heightData;
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
	internal static async Task VerifySystemTableStateProof(
		IClient client,
		int atHeight,
		StateProof stateProof,
		int value)
	{
		var generalApi = new GeneralApi(client);
		
		// Get the table id
		var tableId =
			ModelUtils.GetTableId(SystemTableName);

		// Get the hash of the table entry value
		var entryValueHash =
			StateProof.DataHash(
				ByteString.AttachUnsafe(BitConverter.GetBytes(value)));

		// Calculate the root hash using table data and validate
		var calculatedRootHash = stateProof.ComputeRootHashFromEntry(
			tableId,
			StateHeightKey,
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