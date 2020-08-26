using Miyabi.Asset.Client;
using Miyabi.Asset.Models;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using System;
using System.Threading.Tasks;
using Utility;

namespace AssetSample
{
    class Program
    {
        const string TableName = "AssetTableSample";

        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // Ver2 implements module system. To enable modules, register is required.
            AssetTypesRegisterer.RegisterTypes();

            await CreateAssetTable(client);
            await GenerateAsset(client);
            await ShowAsset(client);
            await MoveAsset(client);
            await ShowAsset(client);

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task CreateAssetTable(IClient client)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);

            // Create entry
            var entry = new CreateTable(new AssetTableDescriptor(
                TableName,
                false,
                false,
                new Address[]
                {
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                }));

            // Create transaction
            var tx = TransactionCreator.CreateTransaction(
                new[] { entry },
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

            // Create gen entry
            var entry = new AssetGen(
                TableName,
                1000,
                new PublicKeyAddress(Utils.GetUser0KeyPair().PublicKey));

            // Create signed transaction with builder. To generate asset,
            // table owner's private key is required.
            var txSigned = TransactionCreator.CreateTransactionBuilder(
                new [] { entry },
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

        private static async Task MoveAsset(IClient client)
        {
            var generalApi = new GeneralApi(client);

            // Create move entry
            var entry = new AssetMove(
                TableName,
                1000,
                new PublicKeyAddress(Utils.GetUser0KeyPair()),
                new PublicKeyAddress(Utils.GetUser1KeyPair()));

            // Using SimpleSignedTransacttion is the easiest way to create
            // simple transactions.
            var txSigned = TransactionCreator.SimpleSignedTransaction(
                entry, Utils.GetUser0KeyPair().PrivateKey);

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

            foreach (var address in addresses)
            {
                var result = await assetClient.GetAssetAsync(TableName, address);
                Console.WriteLine($"address={address}, amount={result.Value}");
            }
        }
    }
}
