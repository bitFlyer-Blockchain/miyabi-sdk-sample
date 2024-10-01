using Miyabi.Asset.Client;
using Miyabi.Asset.Models;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using System;
using System.Threading.Tasks;
using Miyabi.ModelSdk.Requests;
using Utility;

namespace CombinedTransaction
{
    class Program
    {
        private static readonly string[] TableNames = new[] {"CoinA", "CoinB"};

        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            AssetTypesRegisterer.RegisterTypes();

            await CreateAssetTable(client);
            await GenerateAsset(client);
            await ShowAsset(client);
            await SwapAsset(client);
            await ShowAsset(client);

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task CreateAssetTable(IClient client)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);

            // Create tables
            var tableA = new CreateAssetTable(
                TableNames[0],
                false,
                false,
                new Address[]
                {
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                });
            var tableB = new CreateAssetTable(
                TableNames[1],
                false,
                false,
                new Address[]
                {
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                });

            // Create transaction
            var tx = TransactionCreator.CreateTransaction(
                new[] { tableA, tableB },
                new[] { new SignatureCredential(
                    Utils.GetTableAdminKeyPair().PublicKey) });

            // Sign transaction. To create a table, TableAdmin's private key is
            // required
            var txSigned = TransactionCreator.SignTransaction(
                tx, new[] { Utils.GetTableAdminKeyPair().PrivateKey });

            // Send transaction
            await generalApi.SendTransactionAsync(txSigned);

            // Wait until the transaction is stored in a block and get the result
            var result = await Utils.WaitTx(generalApi, tx.Id);
            Console.WriteLine($"txid={tx.Id}, result={result}");
        }

        private static async Task GenerateAsset(IClient client)
        {
            var generalApi = new GeneralApi(client);

            // Create generate asset entry.
            var coinA = new AssetGen(
                TableNames[0],
                1000,
                new PublicKeyAddress(Utils.GetUser0KeyPair().PublicKey));
            var coinB = new AssetGen(
                TableNames[1],
                2000,
                new PublicKeyAddress(Utils.GetUser1KeyPair().PublicKey));

            // Create signed transaction with builder. To generate asset,
            // table owner's private key is required.
            var txSigned = TransactionCreator.CreateTransactionBuilder(
                new [] { coinA, coinB },
                new []
                {
                    new SignatureCredential(Utils.GetOwnerKeyPair().PublicKey)
                })
                .Sign(Utils.GetOwnerKeyPair().PrivateKey)
                .Build();

            await generalApi.SendTransactionAsync(txSigned);

            var result = await Utils.WaitTx(generalApi, txSigned.Id);
            Console.WriteLine($"txid={txSigned.Id}, result={result}");
        }

        private static async Task SwapAsset(IClient client)
        {
            var generalApi = new GeneralApi(client);

            // Move CoinA
            var coinA = new AssetMove(
                TableNames[0],
                100,
                new PublicKeyAddress(Utils.GetUser0KeyPair()),
                new PublicKeyAddress(Utils.GetUser1KeyPair()));
            // Move CoinB
            var coinB = new AssetMove(
                TableNames[1],
                200,
                new PublicKeyAddress(Utils.GetUser1KeyPair()),
                new PublicKeyAddress(Utils.GetUser0KeyPair()));

            var txSigned = TransactionCreator.CreateTransactionBuilder(
                new [] { coinA, coinB },
                new []
                {
                    new SignatureCredential(Utils.GetUser0KeyPair().PublicKey),
                    new SignatureCredential(Utils.GetUser1KeyPair().PublicKey),
                })
                .Sign(Utils.GetUser0KeyPair().PrivateKey)
                .Sign(Utils.GetUser1KeyPair().PrivateKey)
                .Build();

            await generalApi.SendTransactionAsync(txSigned);

            var result = await Utils.WaitTx(generalApi, txSigned.Id);
            Console.WriteLine($"txid={txSigned.Id}, result={result}");
        }

        private static async Task ShowAsset(IClient client)
        {
            // AssetClient has access to asset endpoints
            var assetClient = new AssetClient(client);

            var addresses = new Address[] {
                new PublicKeyAddress(Utils.GetUser0KeyPair()),
                new PublicKeyAddress(Utils.GetUser1KeyPair()),
            };
            foreach (var tableName in TableNames)
            {
	            var request = new EntriesRequest<Address>(tableName, addresses);
	            var response = await assetClient.GetAssetsAsync(request);
	            var accountBalances = response.Value;

	            foreach (var accountBalance in accountBalances)
	            {
		            var balance = accountBalance.Value.Data != null ?
			            accountBalance.Value.Data.ToString() :
			            accountBalance.Value.ApiError.ErrorCode.ToString();
		            Console.WriteLine(
			            $"Table='{tableName}', " +
			            $"Account Address='{accountBalance.Key}', " +
			            $"Account balance='{balance}'");
	            }
            }
        }
    }
}
