using Miyabi;
using Miyabi.Asset.Models;
using Miyabi.Binary.Models;
using Miyabi.ClientSdk;
using Miyabi.Contract.Models;
using Miyabi.Entity.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Miyabi.ClientSdk.Client;
using Miyabi.Common;
using Miyabi.Common.Models;
using Miyabi.NFT.Models;
using Miyabi.PrivateData.Models;
using Newtonsoft.Json.Linq;
using Utility;

namespace GeneralApiCallSample
{
    class Program
    {
        const string TableName = "GeneralApiCallSampleTable";

        static readonly ByteString Key0 = ByteString.Encode("key0");
        static readonly ByteString Key1 = ByteString.Encode("key1");

        /// <summary>
        /// Demonstrates the general-purpose blockchain API surface:
        /// listing tables, node/network info, batch transaction
        /// submission, transaction/block lookup by id or
        /// height, and per-entry change history.
        /// </summary>
        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);
            var generalApi = new GeneralApi(client);

            // In order to use a miyabi module, registering types is required.
            CommonTypesRegisterer.RegisterCommonTypes();
            AssetTypesRegisterer.RegisterTypes();
            BinaryTypesRegisterer.RegisterTypes();
            EntityTypesRegisterer.RegisterTypes();
            ContractTypesRegisterer.RegisterTypes();
            NFTTypesRegisterer.RegisterTypes();
            PrivateDataTypesRegisterer.RegisterTypes();
            ContractRegistration.Initialize();

            // List every table on the blockchain
            await GetTables(generalApi);

            // Show general node/network status
            await ShowNodeAndNetworkInfo(generalApi);

            // Create a tracked entity table and add two entries as a single
            // batched send, to get a real transaction id to look up below.
            var txId = await CreateTrackedEntityTableAndAddEntries(generalApi);

            // Look up that transaction by id
            var height = await ShowTransactionDetails(generalApi, txId);

            // Inspect the block it landed in
            await ShowBlockDetails(generalApi, height);

            // Read back its entry's change history
            await ShowEntryHistory(generalApi, Key0);

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task GetTables(GeneralApi generalApi)
        {
            var result = await generalApi.GetTablesAsync();
            foreach (var item in result.Value)
            {
                Console.WriteLine($"table name={item.Name}");
            }
        }

        private static async Task ShowNodeAndNetworkInfo(GeneralApi generalApi)
        {
            var nodeInfo = (await generalApi.GetNodeInfoAsync()).Value;
            var parameters = (await generalApi.GetBlockchainParametersAsync()).Value;
            var height = (await generalApi.GetHeightAsync()).Value;
            var peerCount = (await generalApi.GetPeerCountAsync()).Value;
            var peers = (await generalApi.GetPeersAsync()).Value;
            var mempoolCount = (await generalApi.GetMempoolCountAsync()).Value;

            Console.WriteLine(
                $"NodeInfo(Name={nodeInfo.Name}, Version={nodeInfo.Version}, " +
                $"Status={nodeInfo.Status}, StartTime={nodeInfo.StartTime}), " +
                $"NetworkName={parameters.NetworkName}, " +
                $"Height={height}, " +
                $"PeerCount={peerCount}, Peers=[{string.Join(", ", peers)}], " +
                $"MempoolCount={mempoolCount}");
        }

        /// <summary>
        /// Creates a table with change-tracking enabled and adds two entries
        /// as separate transactions sent together in a single batch, to
        /// demonstrate <see cref="GeneralApi.SendTransactionsAsync"/>.
        /// </summary>
        private static async Task<ByteString>
            CreateTrackedEntityTableAndAddEntries(GeneralApi generalApi)
        {
            // Create entry. `tracked: true` is required for GetHistoryAsync/
            // GetHistoryDetailAsync to return anything for this table's entries.
            var createTableEntry = new CreateEntityTable(
                TableName,
                true,
                false,
                [
                    new PublicKeyAddress(
                        Utils.GetOwnerKeyPair().PublicKey)
                ]);
            var createTableTx = TransactionCreator.CreateTransaction(
                [createTableEntry],
                [
                    new SignatureCredential(
                    Utils.GetTableAdminKeyPair().PublicKey)
                ]);
            var createTableTxSigned = TransactionCreator.SignTransaction(
                createTableTx, [Utils.GetTableAdminKeyPair().PrivateKey]);

            await generalApi.SendTransactionAsync(createTableTxSigned);

            // Wait until the transaction is stored in a block and get the result
            var createTableResult = await Utils.WaitTx(
                generalApi, createTableTxSigned.Id);
            Console.WriteLine(
                $"txid={createTableTxSigned.Id}, result={createTableResult}");

            // Build two independent add-entity transactions.
            var ownerKeyPair = Utils.GetOwnerKeyPair();
            var tx0 = TransactionCreator.CreateTransactionBuilder(
                    [new AddEntity("data0", TableName, Key0)],
                    [new SignatureCredential(ownerKeyPair.PublicKey)])
                .Sign(ownerKeyPair.PrivateKey)
                .Build();
            var tx1 = TransactionCreator.CreateTransactionBuilder(
                    [new AddEntity("data1", TableName, Key1)],
                    [new SignatureCredential(ownerKeyPair.PublicKey)])
                .Sign(ownerKeyPair.PrivateKey)
                .Build();

            // Send both transactions to the node in a single batch call,
            // instead of one SendTransactionAsync call per transaction.
            await generalApi.SendTransactionsAsync([tx0, tx1]);

            // Wait until each transaction in the batch is stored in a block
            // and get the result.
            Console.WriteLine("Batch send results:");
            var result0 = await Utils.WaitTx(generalApi, tx0.Id);
            Console.WriteLine($"txid={tx0.Id}, result={result0}");

            var result1 = await Utils.WaitTx(generalApi, tx1.Id);
            Console.WriteLine($"txid={tx1.Id}, result={result1}");

            return tx0.Id;
        }

        /// <summary>
        /// Looks up a transaction's execution result and its full contents
        /// by id, and returns the height of the block it landed in.
        /// </summary>
        /// <remarks>
        /// A committed transaction reports its own location in the chain as
        /// `txPtr` (part of the same info fetched to print below), which is
        /// absent while the transaction is still pending.
        /// </remarks>
        private static async Task<int> ShowTransactionDetails(
            GeneralApi generalApi, ByteString txId)
        {
            var result = (await generalApi.GetTransactionResultAsync(txId)).Value;
            var infoJson = (await generalApi.GetTransactionInfoJsonAsync(txId)).Value;

            Console.WriteLine(
                $"TxId={txId}, ResultCode={result.ResultCode}\n" +
                $"TxInfo={infoJson}");

            var txPtr = infoJson["txPtr"];
            if (txPtr == null)
            {
                throw new InvalidOperationException(
                    $"Transaction {txId} is not committed to a block.");
            }

            return txPtr["height"].Value<int>();
        }

        private static async Task ShowBlockDetails(
            GeneralApi generalApi, int height)
        {
            var header = (await generalApi.GetHeaderAsync(height)).Value;
            var block = (await generalApi.GetBlockAsync(height)).Value;
            var txsInBlock = (await generalApi.GetTransactionListAsync(height)).Value;

            Console.WriteLine(
                $"Height={height}, StateHash={header.Header.StateHash}, " +
                $"BlockId={block.Id}, TxCount={txsInBlock.Count()}");
        }

        private static async Task ShowEntryHistory(
            GeneralApi generalApi, ByteString key)
        {
            // Requires the table to have been created with `tracked: true`.
            var txIds =
                (await generalApi.GetHistoryAsync(TableName, key.ToString()))
                    .Value.ToList();
            var txDetails =
                (await generalApi.GetHistoryDetailAsync(TableName, key.ToString()))
                    .Value.ToList();

            Console.WriteLine(
                $"Table='{TableName}', Key='{key}', " +
                $"History txIds=[{string.Join(", ", txIds)}], " +
                $"History tx count={txDetails.Count}");
        }
    }
}
