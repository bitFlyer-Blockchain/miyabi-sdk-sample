using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using Miyabi.Cryptography;

namespace Utility
{
	/// <summary>
	/// Utils for sample.
	/// </summary>
	public class Utils
	{
		/// <summary>
		/// Pdo member's public key (node's consensus public key).
		/// </summary>
		private const string PdoPublicKey =
			"026beca06739461a20249fd4b24cf27542013624d18710bc418e8de10d6ef68568";

		/// <summary>
		/// Pdo member's url.
		/// </summary>
		private const string PdoUrl = "https://localhost:7000";

		/// <summary>
		/// Gets API url.
		/// </summary>
		public static string ApiUrl => "https://localhost:9010";

		/// <summary>
		/// Gets PdoMembers.
		/// </summary>
		public static IReadOnlyCollection<PdoMember> PdoMembers =>
			new List<PdoMember>(
				new[]
				{
					new PdoMember(ByteString.Parse(PdoPublicKey), PdoUrl),
				});

		/// <summary>
		/// Table admin private key according to miyabi blockchain-config.
		/// </summary>
		/// <returns>Table admin key pair.</returns>
		public static KeyPair GetTableAdminKeyPair() =>
			GetKeyPair("10425b7e6ebf5e0d5918717f77ce8a66aaf92bc64b65996f885ff12bd94ef529");

		/// <summary>
		/// Contract admin private key according to miyabi blockchain-config.
		/// </summary>
		/// <returns>Contract admin key pair.</returns>
		public static KeyPair GetContractAdminKeyPair() =>
			GetKeyPair("14e3a2d16c8a43a4eb1b088b32bca2abaf274e3f185afc9c15b33491c8deb9a6");

		/// <summary>
		/// Represents table owner or contract owner.
		/// </summary>
		/// <returns>Test owner key pair.</returns>
		public static KeyPair GetOwnerKeyPair() =>
			GetKeyPair("0000000000000000000000000000000000000000000000000000000000000001");

		/// <summary>
		/// Test user1.
		/// </summary>
		/// <returns>Test user key pair.</returns>
		public static KeyPair GetUser0KeyPair() =>
			GetKeyPair("0000000000000000000000000000000000000000000000000000000000000010");

		/// <summary>
		/// Test user2.
		/// </summary>
		/// <returns>Test user key pair.</returns>
		public static KeyPair GetUser1KeyPair() =>
			GetKeyPair("0000000000000000000000000000000000000000000000000000000000000020");

		/// <summary>
		/// Get key pair from privateKey.
		/// </summary>
		/// <param name="privateKey">private key's hex string.</param>
		/// <returns>Test user key pair.</returns>
		public static KeyPair GetKeyPair(string privateKey)
		{
			var adminPrivateKey =
				PrivateKey.Parse(privateKey);
			return new KeyPair(adminPrivateKey);
		}

		/// <summary>
		/// Wait return until tx is finished.
		/// </summary>
		/// <param name="api">General api object.</param>
		/// <param name="id">txid.</param>
		/// <returns>transaction's result code.</returns>
		public static async Task<string> WaitTx(GeneralApi api, ByteString id)
		{
			while (true)
			{
				var result = await api.GetTransactionResultAsync(id);
				if (result.Value.ResultCode != TransactionResultCode.Pending)
				{
					return result.Value.ResultCode.ToString();
				}

				Thread.Sleep(100);
			}
		}

		/// <summary>
		/// Create http handler for bypassing cert issue.
		/// </summary>
		/// <returns>handler.</returns>
		public static HttpClientHandler GetBypassRemoteCertificateValidationHandler()
		{
			var handler = new HttpClientHandler
			{
				ClientCertificateOptions = ClientCertificateOption.Manual,
				ServerCertificateCustomValidationCallback =
					(httpRequestMessage, cert, cetChain, policyErrors) => true,
			};

			return handler;
		}
	}
}
