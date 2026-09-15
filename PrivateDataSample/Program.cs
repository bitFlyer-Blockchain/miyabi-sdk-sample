using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Hash;
using Miyabi.Common.Models;
using Miyabi.Cryptography;
using Miyabi.PrivateData.Client;
using Miyabi.PrivateData.Models;
using Miyabi.PrivateData.Models.Payloads;
using Miyabi.Serialization;
using Newtonsoft.Json;
using Utility;

namespace PrivateDataSample
{
    internal class Program
    {
        /// <summary>
        /// Demonstrates the PrivateData module: creating a table and adding
        /// a row through the PDO (Private Data Owner) evidence flow, then
        /// reading the row back both raw (from the PDO) and hashed (from
        /// blockchain world state), and checking existence/listing entries
        /// using both input forms.
        /// </summary>
        static async Task Main(string[] args)
        {
            var config = new SdkConfig([Utils.ApiUrl],
                privateChannel: new PrivateChannel(Utils.PdoMembers));
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            PrivateDataTypesRegisterer.RegisterTypes();

            // Create private data table
            var tableName = "PrivateDataSampleTable";
            await CreatePrivateDataTable(client, tableName);

            var key = ByteString.Encode("key");
            var value = ByteString.Encode("value");

            // Add private data row
            await AddPrivateData(client, tableName, key, value);

            // Get raw private data from private state of pdo member
            await GetPrivateData(client, tableName, key, false);

            // Get hashed private data from world state of blockchain
            await GetPrivateData(client, tableName, key, true);

            // Check table/entry existence, using both raw and hashed inputs
            await ShowPrivateDataTableExistence(client, tableName, key);

            // List all entries in the table
            await ShowPrivateDataTableEntries(client, tableName);

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task CreatePrivateDataTable(
            IClient client,
            string tableName)
        {
            var pdoMembers = Utils.PdoMembers;
            var tableAdminPrivateKey = Utils.GetTableAdminKeyPair().PrivateKey;
            var hashedTableName = PrivateDataModelUtils.ComputeHash(tableName);

            // Create entry
            var entry = new CreatePrivateDataTable(
                // Need to pass hashed data
                hashedTableName,
                false,
                false,
                [
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                ],
                false,
                PermissionModel.TableOrRow,
                pdoMembers.Select(x => Address.Decode(x.Admin)).ToList());

            // It must have pdo members and table admin to create a table
            var requiredCredentials =
                pdoMembers.Select(x => Credential.Decode(x.Admin))
                    .Append(TransactionCreator.AsCredential(tableAdminPrivateKey));

            // Create private transaction
            // It needs to obtain evidences from PDOs to verify a transaction itself.
            var unsignedTx =
                TransactionCreator.CreateTransaction(
                    [entry], requiredCredentials);
            var payloadEntry = new CreatePrivateDataTablePayload(tableName);
            var privateTx = new PrivateTransaction(
                MessageConverter.Serialize(unsignedTx),
                new List<ByteString>
                {
                    MessageConverter.Serialize(payloadEntry),
                });

            var (txId, txResult) =
                await SendAndWaitPrivateTx(
                    client,
                    privateTx,
                    [tableAdminPrivateKey]);

            Console.WriteLine($"{nameof(CreatePrivateDataTable)} result is:");
            Console.WriteLine(JsonConvert.SerializeObject(new
            {
                TxId = txId,
                Result = txResult,
                TableName = tableName,
                HashedTableName = hashedTableName,
            }, Formatting.Indented));
        }

        private static async Task AddPrivateData(
            IClient client,
            string tableName,
            ByteString key,
            ByteString value)
        {
            var pdoMembers = Utils.PdoMembers;
            var insertAdminPrivateKey = Utils.GetOwnerKeyPair().PrivateKey;
            var rowDataOwnerAddress =
                new PublicKeyAddress(Utils.GetUser0KeyPair().PublicKey);
            var hashedTableName = HashService.ComputeSHA256(tableName);

            // Create entry
            var hashedHexKey = PrivateDataModelUtils.ComputeHash(key);
            var hashedHexValue = PrivateDataModelUtils.ComputeHash(value);
            var entry = new AddPrivateData(
                // Need to pass hashed data since these data are exposed in all miyabi
                hashedTableName,
                hashedHexKey,
                hashedHexValue,
                [rowDataOwnerAddress],
                pdoMembers.Select(x => Address.Decode(x.Admin)));

            // Pdo members also must be included into required credentials
            var requiredCredentials =
                pdoMembers.Select(x => Credential.Decode(x.Admin))
                    .Append(TransactionCreator.AsCredential(insertAdminPrivateKey));

            // Create private transaction
            // It needs to obtain evidences from PDOs to verify a transaction itself.
            var unsignedTx =
                TransactionCreator.CreateTransaction(
                    [entry],
                    requiredCredentials);
            var payloadEntry =
                // These data finally register into PDO members
                // It means only PDO members can return these raw data
                new AddPrivateDataPayload(tableName, key, value);
            var privateTx = new PrivateTransaction(
                MessageConverter.Serialize(unsignedTx),
                new List<ByteString>
                {
                    MessageConverter.Serialize(payloadEntry),
                });

            var (txId, txResult) =
                await SendAndWaitPrivateTx(
                    client,
                    privateTx,
                    [insertAdminPrivateKey]);

            Console.WriteLine($"{nameof(AddPrivateData)} result is:");
            Console.WriteLine(JsonConvert.SerializeObject(new
            {
                TxId = txId,
                Result = txResult,
                TableName = tableName,
                HashedTableName = hashedTableName,
                Key = key,
                HashedHexKey = hashedHexKey,
                Value = value,
                HashedHexValue = hashedHexValue
            }, Formatting.Indented));
        }

        private static async Task GetPrivateData(
            IClient client,
            string tableName,
            ByteString key,
            bool showHash)
        {
            var lookupTableName = showHash
                ? PrivateDataModelUtils.ComputeHash(tableName)
                : tableName;
            var lookupKey = showHash
                ? PrivateDataModelUtils.ComputeHash(key)
                : key;

            var pdEntry = (await new PrivateDataClient(
                    client,
                    Utils.GetBypassRemoteCertificateValidationHandler())
                .GetPrivateDataEntryAsync(lookupTableName, lookupKey, showHash))
                .Value;

            if (pdEntry == null)
            {
                return;
            }

            Console.WriteLine($"{nameof(GetPrivateData)} result is:");
            Console.WriteLine(JsonConvert.SerializeObject(new
            {
                ShowHash = showHash,
                Key = pdEntry.Key,
                Value = pdEntry.Value,
            }, Formatting.Indented));
        }

        private static async Task ShowPrivateDataTableExistence(
            IClient client,
            string tableName,
            ByteString key)
        {
            var privateDataClient = new PrivateDataClient(
                client,
                Utils.GetBypassRemoteCertificateValidationHandler());

            var hashedTableName = PrivateDataModelUtils.ComputeHash(tableName);
            var hashedKey = PrivateDataModelUtils.ComputeHash(key);

            // Raw table name, forwarded to the PDO members to check.
            var rawTableExists =
                (await privateDataClient.CheckPrivateDataTableAsync(
                    tableName, false)).Value;

            // Raw entry key, forwarded to the PDO members to check.
            var rawEntryExists =
                (await privateDataClient.CheckPrivateDataEntryAsync(
                    tableName, key, false)).Value;

            // Hashed table name, checked directly against blockchain world
            // state.
            var hashedTableExists =
                (await privateDataClient.CheckPrivateDataTableAsync(
                    hashedTableName, true)).Value;

            // Hashed entry key, checked directly against blockchain world
            // state.
            var hashedEntryExists =
                (await privateDataClient.CheckPrivateDataEntryAsync(
                    hashedTableName, hashedKey, true)).Value;

            Console.WriteLine(
                $"Table='{tableName}' exists (raw)={rawTableExists}, " +
                $"(hashed)={hashedTableExists}; Key='{key}' entry exists" +
                $" (raw)={rawEntryExists}, (hashed)={hashedEntryExists}");
        }

        private static async Task ShowPrivateDataTableEntries(
            IClient client,
            string tableName)
        {
            var privateDataClient = new PrivateDataClient(
                client,
                Utils.GetBypassRemoteCertificateValidationHandler());

            var hashedTableName = PrivateDataModelUtils.ComputeHash(tableName);

            // Raw table name, resolved through the PDO members - returns
            // the actual key/value data.
            var rawEntries =
                (await privateDataClient.GetPrivateTableDataEntriesAsync(
                    tableName, false)).Value;
            foreach (var entry in rawEntries)
            {
                Console.WriteLine(
                    $"Table='{tableName}' (raw), Key='{entry.Key}', " +
                    $"Value='{entry.Value}'");
            }

            // Hashed table name, read directly from blockchain world state -
            // key/value are returned hashed, since that's all the chain stores.
            var hashedEntries =
                (await privateDataClient.GetPrivateTableDataEntriesAsync(
                    hashedTableName, true)).Value;
            foreach (var entry in hashedEntries)
            {
                Console.WriteLine(
                    $"Table='{tableName}' (hashed), Key='{entry.Key}', " +
                    $"Value='{entry.Value}'");
            }
        }

        private static async Task<(ByteString txId, string txResult)>
            SendAndWaitPrivateTx(
                IClient client,
                PrivateTransaction privateTx,
                IEnumerable<PrivateKey> signingPrivateKeys)
        {
            // Obtain evidences for a private tx signed by all PDOs
            var evidences = await new PrivateDataClient(
                    client,
                    Utils.GetBypassRemoteCertificateValidationHandler(),
                    false)
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
}
