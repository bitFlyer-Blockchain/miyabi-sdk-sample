using Miyabi.Asset.Models;
using Miyabi.Common.Models;
using Miyabi.Contract.Models;
using Miyabi.ContractSdk;

namespace SmartContractSample.ContractFiles
{
	/// <summary>
	/// A sample miyabi smart contract
	/// The contract explains how to create
	/// a fungible digital token application using miyabi's Asset module.
	///
	/// To simplify things, the contract itself will act as
	/// admin for various operations and so need for
	/// externally providing signatures would be reduced.
	///
	/// The following operations will be supported:
	/// - Register an account address (Invokable)
	/// - Delete an account address (Invokable)
	/// - Check if an account is registered (Queryable)
	/// - Generate the digital fungible token for an account (Invokable)
	/// - Transfer the digital fungible token among accounts (Invokable)
	/// - Get current balance of a registered account (Queryable)
	/// - Get name of Asset table which is managing the digital
	///   fungible tokens (Queryable)
	/// </summary>
	public class SampleContract : ContractBase
	{
		static readonly string s_tableNamePrefix = "SampleAssetTokenTable";

		public SampleContract(ContractInitializationContext ctx)
			: base(ctx)
		{
		}

		public override bool Instantiate(string[] args)
		{
			// Do not accept any arguments
			if (args.Length >= 1)
			{
				return false;
			}

			// Make the contract address as the table owner of the Asset table
			// Contract address will also be the token admin for the Asset table
			var tableOwnerAddresses =
				new[] { ContractAddress.FromInstanceId(InstanceId) };

			// Define the table descriptor for the Asset table
			var assetTableDescriptor = new AssetTableDescriptor(
				GetAssetTableName(),
				false,
				false,
				tableOwnerAddresses,
				false,
				PermissionModel.TableAndRow);

			// Try to create the unique Asset table for the contract instance
			try
			{
				StateWriter.AddTable(assetTableDescriptor);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public override bool Delete()
		{
			// Delete the Asset table that was created
			// at the contract instantiation time
			try
			{
				StateWriter.DeleteTable(GetAssetTableName());
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Invokable method.
		/// Register an account with zero balance
		/// Contract address is Table owner and Token admin
		/// So, no extra signature are required for this operation
		/// </summary>
		/// <param name="toRegister">Account address</param>
		public void RegisterAccount(Address toRegister)
		{
			string tableName = GetAssetTableName();
			ExecutionContext.StateWriter
				.TryGetTableWriter<IPermissionedAssetTableWriter>(
					tableName,
					out var permissionedAssetTableWriter);

			permissionedAssetTableWriter.RegisterAccount(toRegister);
		}

		/// <summary>
		/// Invokable method.
		/// Delete an account which has zero balance
		/// Requires signature of the `toDelete` address
		/// </summary>
		/// <param name="toDelete">Account address</param>
		public void DeleteAccount(Address toDelete)
		{
			string tableName = GetAssetTableName();
			ExecutionContext.StateWriter
				.TryGetTableWriter<IPermissionedAssetTableWriter>(
					tableName,
					out var permissionedAssetTableWriter);

			permissionedAssetTableWriter.DeleteAccount(toDelete);
		}

		/// <summary>
		/// Invokable method.
		/// Generate asset token for an account
		/// If the account is not registered,
		/// it will be automatically registered
		/// Contract address is Table owner and Token admin
		/// So, no extra signature are required for this operation
		/// </summary>
		/// <param name="toAddress">To account address</param>
		/// <param name="amount">Amount</param>
		public void GenerateAssetToken(Address toAddress, decimal amount)
		{
			string tableName = GetAssetTableName();
			ExecutionContext.StateWriter.TryGetTableWriter<IPermissionedAssetTableWriter>(
				tableName,
				out var permissionedAssetTableWriter);

			permissionedAssetTableWriter.MintTokens(toAddress, amount);
		}

		/// <summary>
		/// Invokable method.
		/// Move asset tokens from one account to another
		/// Requires signature of `fromAddress` address
		/// </summary>
		/// <param name="fromAddress">From account address</param>
		/// <param name="toAddress">To account address</param>
		/// <param name="amount">Amount to be transferred</param>
		public void MoveAssetToken(
			Address fromAddress,
			Address toAddress,
			decimal amount)
		{
			string tableName = GetAssetTableName();
			ExecutionContext.StateWriter
				.TryGetTableWriter<IPermissionedAssetTableWriter>(
				tableName,
				out var permissionedAssetTableWriter);

			permissionedAssetTableWriter.TransferTokens(
				fromAddress,
				toAddress,
				amount);
		}

		/// <summary>
		/// Queryable method.
		/// Returns the balance of a registered account address
		/// </summary>
		/// <param name="accountAddress">Account address</param>
		/// <returns>Balance as decimal</returns>
		public decimal GetBalanceOf(Address accountAddress)
		{
			string tableName = GetAssetTableName();

			ExecutionContext.StateWriter
				.TryGetTableReader<IPermissionedAssetTableWriter>(
				tableName,
				out var permissionedAssetTableWriter);
			permissionedAssetTableWriter.TryGetBalance(
				accountAddress,
				out var balance);

			return balance;
		}

		/// <summary>
		/// Queryable method.
		/// Returns whether or not the account has been registered in the table
		/// </summary>
		/// <param name="accountAddress">Account address</param>
		/// <returns>Bool</returns>
		public bool IsAccountRegistered(Address accountAddress)
		{
			string tableName = GetAssetTableName();

			ExecutionContext.StateWriter
				.TryGetTableReader<IPermissionedAssetTableReader>(
				tableName,
				out var permissionedAssetTableReader);

			return permissionedAssetTableReader
				.IsAccountRegistered(accountAddress);
		}

		/// <summary>
		/// Queryable method.
		/// Returns the name of the Asset table
		/// </summary>
		/// <returns>Name of the Asset table</returns>
		public string GetAssetTableName()
		{
			return s_tableNamePrefix + "_" + InstanceName;
		}
	}
}