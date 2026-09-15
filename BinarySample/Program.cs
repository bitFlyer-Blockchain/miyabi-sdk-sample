using Miyabi;
using Miyabi.Binary.Client;
using Miyabi.Binary.Models;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Models;
using Miyabi.ModelSdk.Requests;
using System;
using System.Linq;
using System.Threading.Tasks;
using Utility;

namespace BinarySample
{
    class Program
    {
        const string TableName = "BinaryTableSample";

        static readonly ByteString Key0 = ByteString.Encode("key0");
        static readonly ByteString Key1 = ByteString.Encode("key1");
        static readonly ByteString Value0 = ByteString.Encode("value0");
        static readonly ByteString Value1 = ByteString.Encode("value1");

        /// <summary>
        /// Demonstrates the Binary module: creating a table, adding raw
        /// binary values, reading them back individually/in batch/in bulk,
        /// and checking table/entry existence.
        /// </summary>
        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            BinaryTypesRegisterer.RegisterTypes();

            // Create the table, then add two entries to it.
            await CreateBinaryTable(client);
            await AddBinaryValue(client, Key0, Value0);
            await AddBinaryValue(client, Key1, Value1);

            // Check table/entry existence
            await ShowBinaryTableExistence(client);

            // Read a single entry
            await ShowBinaryValue(client, Key0);

            // Read a batch of entries in one call
            await ShowBinaryValues(client, [Key0, Key1]);

            // Dump the full table content
            await ShowBinaryTable(client);

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task CreateBinaryTable(IClient client)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);

            // Create entry
            var entry = new CreateBinaryTable(
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

        private static async Task AddBinaryValue(
            IClient client, ByteString key, ByteString value)
        {
            var generalApi = new GeneralApi(client);

            // Create binary add-value entry.
            var entry = new BinaryAddValue(
                TableName,
                key,
                value,
                [new PublicKeyAddress(Utils.GetUser0KeyPair().PublicKey)]);

            // Create signed transaction with builder. To add a value,
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

        private static async Task ShowBinaryTableExistence(IClient client)
        {
            // BinaryClient has access to binary endpoints
            var binaryClient = new BinaryClient(client);

            // CheckBinaryTableAsync/CheckBinaryEntryAsync return true/false —
            // no need to fetch the actual data just to know it exists.
            var tableExists =
                (await binaryClient.CheckBinaryTableAsync(TableName)).Value;
            var key0Exists =
                (await binaryClient.CheckBinaryEntryAsync(TableName, Key0)).Value;

            // A key that was never added, to show the false case too.
            var unknownKeyExists = (await binaryClient.CheckBinaryEntryAsync(
                TableName, ByteString.Encode("no-such-key"))).Value;

            Console.WriteLine(
                $"Table='{TableName}' exists={tableExists}, " +
                $"key='{Key0}' exists={key0Exists}, " +
                $"unknown key exists={unknownKeyExists}");
        }

        private static async Task ShowBinaryValue(IClient client, ByteString key)
        {
            var binaryClient = new BinaryClient(client);

            // Get the raw value of a single entry
            var value = (await binaryClient.GetBinaryValue(TableName, key)).Value;

            // Get the data owners of the same entry
            var owners =
                (await binaryClient.GetBinaryOwnersAsync(TableName, key))
                    .Value.RowOwners;

            Console.WriteLine(
                $"Table='{TableName}', Key='{key}', Value='{value}', " +
                $"Owners=[{string.Join(", ", owners)}]");
        }

        private static async Task ShowBinaryValues(
            IClient client, ByteString[] keys)
        {
            var binaryClient = new BinaryClient(client);

            // Batch fetch multiple entries in a single call. Entries that
            // fail to resolve carry an ApiError instead of Data.
            var request = new EntriesRequest<ByteString>(TableName, keys);
            var response = await binaryClient.GetBinaryValues(request);

            foreach (var entry in response.Value)
            {
                var value = entry.Value.Data != null
                    ? entry.Value.Data.ToString()
                    : entry.Value.ApiError.ErrorCode.ToString();
                Console.WriteLine(
                    $"Table='{TableName}', Key='{entry.Key}', Value='{value}'");
            }
        }

        private static async Task ShowBinaryTable(IClient client)
        {
            var binaryClient = new BinaryClient(client);

            var table = (await binaryClient.GetBinaryTable(TableName)).Value;

            foreach (var (key, value) in table)
            {
                Console.WriteLine(
                    $"Table='{TableName}', Key='{key}', Value='{value}'");
            }
        }
    }
}
