using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using Miyabi.Contract.Client;
using Miyabi.Contract.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SmartContractSample.ContractFiles;
using Utility;
using Miyabi.Cryptography;

namespace SmartContractSample
{
	/// <summary>
	/// Program.cs deploys, instantiates, invokes and queries a smart contracts.
	/// </summary>
	class Program
	{
		const string ContractFilePath = "ContractFiles\\SampleContract.cs";
		static readonly string ContractName =
			typeof(SampleContract).FullName;
		
		const string InstanceName = "SampleContractInstance";
		static readonly KeyPair TableAdmin =
			Utils.GetTableAdminKeyPair();
		static readonly KeyPair ContractAdmin =
			Utils.GetContractAdminKeyPair();
		static readonly KeyPair ContractInstanceOwner = Utils.GetOwnerKeyPair();

		static readonly ByteString AssemblyId =
			ContractUtils.GetAssemblyId(new[]
			{
				File.ReadAllText(ContractFilePath)
			});

		static async Task Main(string[] args)
		{
			var handler = Utils.GetBypassRemoteCertificateValidationHandler();

			var config = new SdkConfig(Utils.ApiUrl);
			var client = new Client(config, handler);
			var generalApi = new GeneralApi(client);
			var contractClient = new ContractClient(client);

			// Account aliases
			var fromAccount = Utils.GetUser0KeyPair();
			var toAccount = Utils.GetUser1KeyPair();

			// In order to use miyabi module, registering types is required.
			ContractTypesRegisterer.RegisterTypes();

			// Deploy the contract files to blockchain
			await DeployContract(generalApi);

			// Create instance of contract using the deployed binaries
			await InstantiateContract(generalApi);

			// Call a queryable method. This operation is read only.
			// Get the name of the Asset table which stores the digital tokens
			await QueryMethod(contractClient,
				nameof(SampleContract.GetAssetTableName),
				"Check Asset table name in miyabi. Expected: SampleAssetTokenTable_SampleContractInstance",
				null);

			// Call an invokable method. This operation can be read-write.
			// Register an account in the Asset table
			await InvokeContract(
				generalApi,
				nameof(SampleContract.RegisterAccount),
				new KeyPair[] { },
				new[] { fromAccount.PublicKey.ToString() });

			// Call a queryable method. This operation is read only.
			// Check whether the account is registered
			await QueryMethod(contractClient,
				nameof(SampleContract.IsAccountRegistered),
				"Check if account registered. Expected: true",
				new[] { fromAccount.PublicKey.ToString() });

			// Call an invokable method. This operation can be read-write.
			// Generate digital tokens
			await InvokeContract(
				generalApi,
				nameof(SampleContract.GenerateAssetToken),
				new KeyPair[] { },
				new[] { fromAccount.PublicKey.ToString(), "100.0" });

			// Call a queryable method. This operation is read only.
			// Check the account balance
			await QueryMethod(contractClient,
				nameof(SampleContract.GetBalanceOf),
				"Get balance of account. Expected: 100.0",
				new[] { fromAccount.PublicKey.ToString() });

			// Call an invokable method. This operation can be read-write.
			// Move the digital tokens amongst different accounts
			await InvokeContract(
				generalApi,
				nameof(SampleContract.MoveAssetToken),
				new[] { fromAccount },
				new[] { fromAccount.PublicKey.ToString(), toAccount.PublicKey.ToString(), "100.0" });

			// Call a queryable method. This operation is read only.
			// Check whether the account is registered
			await QueryMethod(contractClient,
				nameof(SampleContract.IsAccountRegistered),
				"Check if account registered. Expected: true",
				new[] { fromAccount.PublicKey.ToString() });

			// Call a queryable method. This operation is read only.
			// Check whether the account is registered
			await QueryMethod(contractClient,
				nameof(SampleContract.IsAccountRegistered),
				"Check if account registered. Expected: true",
				new[] { toAccount.PublicKey.ToString() });

			// Call an invokable method. This operation can be read-write.
			// Delete an account
			await InvokeContract(
				generalApi,
				nameof(SampleContract.DeleteAccount),
				new[] { fromAccount },
				new[] { fromAccount.PublicKey.ToString() });

			// Call a queryable method. This operation is read only.
			// Check whether the account is registered
			await QueryMethod(contractClient,
				nameof(SampleContract.IsAccountRegistered),
				"Check if account registered. Expected: false",
				new[] { fromAccount.PublicKey.ToString() });
			
			// Delete the contract instance.
			// This will perform related table and data delete also.
			await DeleteContractInstance(generalApi);
			
			Console.WriteLine("Press enter to exit");
			Console.ReadLine();
		}

		private static async Task DeployContract(GeneralApi generalApi)
		{
			Console.WriteLine($"Deploying contract {ContractName}");

			// Create entry
			var sources = new[]
			{
				await File.ReadAllTextAsync(ContractFilePath)
			};
			var instantiators = new[]
			{
				new PublicKeyAddress(ContractInstanceOwner.PublicKey)
			};
			var entry =
				new ContractDeploy(sources, instantiators: instantiators);

			// Create transaction
			var tx = TransactionCreator.CreateTransaction(
				new[] { entry },
				new[]
				{
					new SignatureCredential(
						ContractAdmin.PublicKey)
				});

			// Sign transaction.
			// Deploy contract requires contract admin signature
			var txSigned = TransactionCreator.SignTransaction(
				tx,
				new[] { ContractAdmin.PrivateKey });

			// Send transaction
			await generalApi.SendTransactionAsync(txSigned);

			// Wait until the transaction is stored in a block and get the result
			var result = await Utils.WaitTx(generalApi, tx.Id);
			Console.WriteLine($"TxId={tx.Id}, Tx Result={result}");
		}

		private static async Task InstantiateContract(GeneralApi generalApi)
		{
			Console.WriteLine($"\nCreate new contract instance: {InstanceName}");

			// Create gen entry
			var instanceOwners = new Address[] { new PublicKeyAddress(ContractInstanceOwner) };
			var entry = new ContractInstantiate(
					AssemblyId,
					ContractName,
					InstanceName,
					instanceOwners);

			// Create signed transaction with builder.
			// To generate instantiate contract,
			// table admin and contract owner private key is required.
			var txSigned = TransactionCreator.CreateTransactionBuilder(
				new[] { entry },
				new[]
				{
					new SignatureCredential(
						TableAdmin.PublicKey),
					new SignatureCredential(ContractInstanceOwner.PublicKey)
				})
				.Sign(TableAdmin.PrivateKey)
				.Sign(ContractInstanceOwner.PrivateKey)
				.Build();

			await generalApi.SendTransactionAsync(txSigned);

			var result = await Utils.WaitTx(generalApi, txSigned.Id);
			Console.WriteLine($"TxId={txSigned.Id}, Tx Result={result}");
		}

		private static async Task InvokeContract(
			GeneralApi generalApi,
			string methodName,
			KeyPair[] signers,
			string[] parameters)
		{
			Console.WriteLine($"\nInvoking contract method {methodName}");

			// Create gen entry
			var entry =
				new ContractInvoke(
					AssemblyId,
					ContractName,
					InstanceName,
					methodName,
					parameters);

			// Create signed transaction with builder.
			// Contract invoke requires contract owner's private key
			var requiredCredentials = new List<SignatureCredential>();

			requiredCredentials
				.AddRange(
					signers.Select(
						keyPair => new SignatureCredential(keyPair.PublicKey)));

			var txBuilder = TransactionCreator.CreateTransactionBuilder(
				new[] { entry },
				requiredCredentials);

			foreach (var keyPair in signers)
			{
				txBuilder.Sign(keyPair.PrivateKey);
			}

			var txSigned = txBuilder.Build();

			await generalApi.SendTransactionAsync(txSigned);

			var result = await Utils.WaitTx(generalApi, txSigned.Id);
			Console.WriteLine($"TxId={txSigned.Id}, Tx Result={result}");
		}

		private static async Task QueryMethod(
			ContractClient contractClient,
			string methodName,
			string outputMessage,
			string[] arguments)
		{
			Console.WriteLine($"\nQuerying contract method: {methodName}");

			var result = await contractClient.QueryContractAsync(
				AssemblyId,
				ContractName,
				InstanceName,
				methodName,
				arguments);
			Console.WriteLine(outputMessage);
			Console.WriteLine($"Query output:={result.Value}");
		}

		private static async Task DeleteContractInstance(GeneralApi generalApi)
		{
			Console.WriteLine($"\nDeleting contract instance: {InstanceName}");

			// Create delete contract instance entry
			var entry = new ContractInstanceDelete(
				AssemblyId,
				ContractName,
				InstanceName);

			// Create signed transaction with builder.
			// Delete contract instance requires
			// signature of the contract instance owners.
			var txSigned = TransactionCreator.CreateTransactionBuilder(
					new[] { entry },
					new[]
					{
                        // Contract Instance Owner
				        new SignatureCredential(ContractInstanceOwner.PublicKey)
					})
				.Sign(ContractInstanceOwner.PrivateKey)
				.Build();

			await generalApi.SendTransactionAsync(txSigned);

			var result = await Utils.WaitTx(generalApi, txSigned.Id);
			Console.WriteLine($"TxId={txSigned.Id}, Tx Result={result}");
		}
	}
}
