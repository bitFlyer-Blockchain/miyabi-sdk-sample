using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using Miyabi.Entity.Client;
using Miyabi.Entity.Models;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Miyabi.ClientSdk.Client;
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

        /// <summary>
        /// Demonstrates the Entity module: creating a table, adding entries,
        /// reading them back, linking a child table to a parent entry via
        /// <see cref="ParentReference"/>, walking the resulting parent/child
        /// tree, and checking table/entry existence.
        /// </summary>
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

            // Create the parent table and add two entries to it.
            await CreateEntityTable(client, TableName);
            await AddEntity(client, TableName, keys[0], "data0");
            await AddEntity(client, TableName, keys[1], "data1");
            await ShowEntity(client, TableName, keys.Take(2).ToArray());

            // Create a child table, then add an entry to it that links back
            // to Key0 in the parent table via a ParentReference. This is
            // what makes the two tables' entries form a tree.
            await CreateEntityTable(client, ChildTableName);
            var pointer = new TableEntryPointer(TableName, ByteString.Parse(Key0));
            var reference = new ParentReference(pointer, "tag0");
            await AddEntity(client, ChildTableName, ByteString.Parse(Key2), "data2", reference);
            await ShowEntity(client, TableName, keys.Take(2).ToArray());
            await ShowEntity(client, ChildTableName, keys.Skip(2).ToArray());

            // Print the tree rooted at TableName/Key0, including its
            // ChildTableName child added above via ParentReference.
            await ShowEntityTree(client, TableName, ByteString.Parse(Key0));

            // Check table/entry existence
            await ShowEntityTableExistence(client, TableName, ByteString.Parse(Key0));

            // Dump the full table content
            await ShowEntityTable(client, TableName);

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

        private static async Task AddEntity(
            IClient client,
            string tableName,
            ByteString key,
            string data,
            ParentReference reference = null)
        {
            var generalApi = new GeneralApi(client);

            // Create add entry
            var entry = new AddEntity(data, tableName, key, reference);

            // Create signed transaction with builder. To add entity,
            // table owner's private key is required.
            var txSigned = TransactionCreator.CreateTransactionBuilder(
                    [entry],
                    [
                        new SignatureCredential(Utils.GetOwnerKeyPair().PublicKey)
                    ])
                .Sign(Utils.GetOwnerKeyPair().PrivateKey)
                .Build();

            // Send transaction
            await generalApi.SendTransactionAsync(txSigned);

            // Wait until the transaction is stored in a block and get the result
            var result = await Utils.WaitTx(generalApi, txSigned.Id);
            Console.WriteLine($"txid={txSigned.Id}, result={result}");
        }

        private static async Task ShowEntity(
            IClient client,
            string tableName,
            ByteString[] keys)
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

        private static async Task ShowEntityTree(
            IClient client,
            string tableName,
            ByteString key)
        {
            var entityClient = new EntityClient(client);

            // Full tree: every node includes its data.
            var tree = await entityClient.GetEntityTreeAsync(tableName, key);
            Console.WriteLine("Entity tree (with data):");
            PrintEntityTree(tree.Value, 0);

            // excludeData: true only returns tableName/entryId per node (no
            // Data/HexData), and silently omits children the caller isn't
            // permitted to read instead of throwing an unauthorized error.
            var treeWithoutData =
                await entityClient.GetEntityTreeAsync(tableName, key, excludeData: true);
            Console.WriteLine("Entity tree (--exclude-data):");
            PrintEntityTree(treeWithoutData.Value, 0);
        }

        private static async Task ShowEntityTableExistence(
            IClient client, string tableName, ByteString key)
        {
            var entityClient = new EntityClient(client);

            // CheckEntityTableAsync/CheckEntityEntryAsync return true/false —
            // no need to fetch the actual data just to know it exists.
            var tableExists =
                (await entityClient.CheckEntityTableAsync(tableName)).Value;
            var entryExists =
                (await entityClient.CheckEntityEntryAsync(tableName, key)).Value;
            Console.WriteLine(
                $"Table='{tableName}' exists={tableExists}, " +
                $"Key='{key}' entry exists={entryExists}");
        }

        private static async Task ShowEntityTable(IClient client, string tableName)
        {
            var entityClient = new EntityClient(client);

            // Dump the full content of the table.
            var table = (await entityClient.GetEntityTableAsync(tableName)).Value;
            foreach (var (entryKey, entity) in table)
            {
                Console.WriteLine(
                    $"Table='{tableName}', Key='{entryKey}', " +
                    $"Data='{entity.Data}'");
            }
        }

        private static void PrintEntityTree(EntityTreeRepresentation node, int depth)
        {
            var indent = new string(' ', depth * 2);
            var comment = string.IsNullOrWhiteSpace(node.Comment)
                ? string.Empty
                : $", comment={node.Comment}";
            var recursive = node.IsRecursive ? ", isRecursive=true" : string.Empty;
            Console.WriteLine(
                $"{indent}tableName={node.TableName}, entryId={node.EntryId}, " +
                $"data={node.Data ?? "<excluded>"}{comment}{recursive}");

            // node.Children itself, a tag's child list, or an entry in it,
            // can each be null when the caller isn't permitted to read that
            // child (see the excludeData note above).
            foreach (var (tag, children) in node.Children ?? [])
            {
                // The tag label sits one level below its node (depth + 1);
                // each child under that tag sits one level below the tag
                // (depth + 2), so it prints deeper than its own label.
                Console.WriteLine($"{indent}  tag={tag}");

                foreach (var child in children ?? [])
                {
                    if (child == null)
                    {
                        continue;
                    }

                    PrintEntityTree(child, depth + 2);
                }
            }
        }
    }
}
