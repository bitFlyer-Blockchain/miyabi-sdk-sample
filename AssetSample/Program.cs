using Miyabi.Asset.Client;
using Miyabi.Asset.Models;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Models;
using System;
using System.Threading.Tasks;
using Miyabi.ModelSdk.Requests;
using Utility;

namespace AssetSample
{
    class Program
    {
        const string TableName = "AssetTableSample";

        /// <summary>
        /// Demonstrates the Asset module: creating a table, minting
        /// an asset to an account, moving it to another
        /// account, reading balances, and checking table/entry existence.
        /// </summary>
        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            AssetTypesRegisterer.RegisterTypes();

            // Create the table, then mint 1000 units to user0.
            await CreateAssetTable(client);
            await GenerateAsset(client);
            await ShowAsset(client);

            // Move the full balance from user0 to user1.
            await MoveAsset(client);
            await ShowAsset(client);

            // Check table/entry existence
            await ShowAssetTableExistence(client);

            // Dump the full table content
            await ShowAssetTable(client);

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task CreateAssetTable(IClient client)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);

            // Create entry
            var entry = new CreateAssetTable(
                TableName,
                false,
                false,
                [
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                ]);

            // Create transaction
            var tx = TransactionCreator.CreateTransaction(
                [entry],
                [
                    new SignatureCredential(
                    Utils.GetTableAdminKeyPair().PublicKey)
                ]);

            // Sign transaction. To create a table, TableAdmin's private key is
            // required
            var txSigned = TransactionCreator.SignTransaction(
                tx, [Utils.GetTableAdminKeyPair().PrivateKey]);

            // Send transaction
            await generalApi.SendTransactionAsync(txSigned);

            // Wait until the transaction is stored in a block and get the result
            var result = await Utils.WaitTx(generalApi, tx.Id);
            Console.WriteLine($"txid={tx.Id}, result={result}");
        }

        private static async Task GenerateAsset(IClient client)
        {
            var generalApi = new GeneralApi(client);

            // Create asset generate entry.
            var entry = new AssetGen(
                TableName,
                1000,
                new PublicKeyAddress(Utils.GetUser0KeyPair().PublicKey));

            // Create signed transaction with builder. To generate asset,
            // table owner's private key is required.
            var txSigned = TransactionCreator.CreateTransactionBuilder(
                    [entry],
                    [
                        new SignatureCredential(Utils.GetOwnerKeyPair().PublicKey)
                    ])
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

            // Using SimpleSignedTransaction is the easiest way to create
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

            var request = new EntriesRequest<Address>(TableName, addresses);
            var response = await assetClient.GetAssetsAsync(request);
            var accountBalances = response.Value;
            foreach (var accountBalance in accountBalances)
            {
                var balance = accountBalance.Value.Data != null ?
                    accountBalance.Value.Data.ToString() :
                    accountBalance.Value.ApiError.ErrorCode.ToString();
                Console.WriteLine(
                    $"Table='{TableName}', " +
                    $"Account Address='{accountBalance.Key}', " +
                    $"Account balance='{balance}'");
            }
        }

        private static async Task ShowAssetTableExistence(IClient client)
        {
            var assetClient = new AssetClient(client);

            // CheckAssetTableAsync/CheckAssetEntryAsync return true/false —
            // no need to fetch the actual data just to know it exists.
            var tableExists =
                (await assetClient.CheckAssetTableAsync(TableName)).Value;
            var user0Address = new PublicKeyAddress(Utils.GetUser0KeyPair());
            var user0EntryExists =
                (await assetClient.CheckAssetEntryAsync(TableName, user0Address))
                    .Value;
            Console.WriteLine(
                $"Table='{TableName}' exists={tableExists}, " +
                $"Account='{user0Address}' entry exists={user0EntryExists}");
        }

        private static async Task ShowAssetTable(IClient client)
        {
            var assetClient = new AssetClient(client);

            // Dump the full content of the table: every address and balance.
            var table = (await assetClient.GetAssetTableAsync(TableName)).Value;
            foreach (var (address, balance) in table)
            {
                Console.WriteLine(
                    $"Table='{TableName}', Address='{address}', " +
                    $"Balance='{balance}'");
            }
        }
    }
}
