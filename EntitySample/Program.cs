using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using Miyabi.Entity.Client;
using Miyabi.Entity.Models;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;

namespace EntitySample
{
    class Program
    {
        const string Key0 = "10";
        const string Key1 = "20";
        const string Key2 = "30";

        const string TableName = "EntityTableSample";
        const string ChildTableName = "ClildEntityTableSample";

        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            var keys = new ByteString[]
            {
                ByteString.Parse(Key0),
                ByteString.Parse(Key1),
                ByteString.Parse(Key2),
            };

            // In order to use a miyabi module, registering types is required.
            EntityTypesRegisterer.RegisterTypes();

            await CreateEntityTable(client, TableName);
            await AddEntity(client, TableName, keys[0], "data0");
            await AddEntity(client, TableName, keys[1], "data1");
            await ShowEntity(client, TableName, keys.Take(2).ToArray());

            await CreateEntityTable(client, ChildTableName);
            var pointer = new TableEntryPointer(TableName, ByteString.Parse(Key0));
            var reference = new ParentReference(pointer, "tag0");
            await AddEntity(client, ChildTableName, ByteString.Parse(Key2), "data2", reference);
            await ShowEntity(client, TableName, keys.Take(2).ToArray());
            await ShowEntity(client, ChildTableName, keys.Skip(2).ToArray());

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task CreateEntityTable(IClient client, string tableName)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);

            // Create entry
            var entry = new CreateEntityTable(
                tableName,
                false,
                false,
                new Address[]
                {
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                });

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

        private static async Task AddEntity(
            IClient client, string tableName, ByteString key, string data, ParentReference reference = null)
        {
            var generalApi = new GeneralApi(client);

            // Create add entry
            var entry = new AddEntity(data, tableName, key, reference);

            // Create signed transaction with builder. To add entity,
            // table owner's private key is required.
            var txSigned = TransactionCreator.CreateTransactionBuilder(
                new [] { entry },
                new []
                {
                    new SignatureCredential(Utils.GetOwnerKeyPair().PublicKey)
                })
                .Sign(Utils.GetOwnerKeyPair().PrivateKey)
                .Build();

            // Send transaction
            await generalApi.SendTransactionAsync(txSigned);

            // Wait until the transaction is stored in a block and get the result
            var result = await Utils.WaitTx(generalApi, txSigned.Id);
            Console.WriteLine($"txid={txSigned.Id}, result={result}");
        }

        private static async Task ShowEntity(IClient client, string tableName, ByteString[] keys)
        {
            // EntityClient has access to entity endpoints
            var entityClient = new EntityClient(client);

            foreach (var key in keys)
            {
                var result = await entityClient.GetEntityAsync(tableName, key);
                Console.WriteLine($"key={key}, value={result.Value.Data}");

                var parents = new StringBuilder();
                foreach (var parent in result.Value.Parents)
                {
                    parents.Append($"[tableName={parent.Parent.TableName}, key={parent.Parent.EntryId}]");
                }

                var children = new StringBuilder();
                foreach (var child in result.Value.Children)
                {
                    children.Append($"[tag={child.Key}");
                    foreach (var entry in child.Value)
                    {
                        children.Append($"[tableName={entry.TableName}, key={entry.EntryId}]");
                    }
                    children.Append($"]");
                }
                Console.WriteLine($"parents={parents}, children={children}");
            }
        }
    }
}
