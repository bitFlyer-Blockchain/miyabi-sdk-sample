using Miyabi.NFT.Client;
using Miyabi.NFT.Models;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using System;
using System.Threading.Tasks;
using Miyabi.ClientSdk.Client;
using Miyabi.ModelSdk.Requests;
using Utility;

namespace NFTSample
{
    class Program
    {
        const string TableName = "NFTTableSample";

        /// <summary>
        /// Demonstrates the NFT module: creating a table, minting a token to
        /// an owner, moving it to another owner, reading balances/ownership,
        /// and checking table/token existence.
        /// </summary>
        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            NFTTypesRegisterer.RegisterTypes();

            var tokenId = "my_token_id";

            // Create the table, then mint one token to user0.
            await CreateNFTTable(client);
            await AddNFT(client, tokenId);
            await ShowNFTBalance(client);

            // Move the token from user0 to user1.
            await MoveNFT(client, tokenId);
            await ShowNFTBalance(client);

            // Check table/token existence
            await ShowNFTTableExistence(client, tokenId);

            // Dump the full table content
            await ShowNFTTable(client);

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task CreateNFTTable(IClient client)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);

            // Create entry
            // tableOwner will be token admin if this entry used.
            var entry = new CreateNFTTable(
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

        private static async Task AddNFT(IClient client, string tokenId)
        {
            var generalApi = new GeneralApi(client);

            // Create nft add entry.
            var entry = new NFTAdd(
                TableName,
                tokenId,
                new PublicKeyAddress(Utils.GetUser0KeyPair().PublicKey));

            // Create signed transaction with builder. To add nft,
            // token admin's private key is required.
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

        private static async Task MoveNFT(IClient client, string tokenId)
        {
            var generalApi = new GeneralApi(client);

            // Create nft move entry
            var entry = new NFTMove(
                TableName,
                tokenId,
                new PublicKeyAddress(Utils.GetUser1KeyPair()));

            // Using SimpleSignedTransaction is the easiest way to create
            // simple transactions.
            var txSigned = TransactionCreator.SimpleSignedTransaction(
                entry, Utils.GetUser0KeyPair().PrivateKey);

            await generalApi.SendTransactionAsync(txSigned);

            var result = await Utils.WaitTx(generalApi, txSigned.Id);
            Console.WriteLine($"txid={txSigned.Id}, result={result}");
        }

        private static async Task ShowNFTBalance(IClient client)
        {
            // nftClient has access to nft endpoints
            var nftClient = new NFTClient(client);

            var addresses = new Address[] {
                new PublicKeyAddress(Utils.GetUser0KeyPair()),
                new PublicKeyAddress(Utils.GetUser1KeyPair()),
            };

            var request = new EntriesRequest<Address>(TableName, addresses);
            var response = await nftClient.GetBalancesAsync(request);
            var accountBalances = response.Value;

            foreach (var accountBalance in accountBalances)
            {
                var balance = accountBalance.Value.Data != null ?
                    accountBalance.Value.Data.ToString() :
                    accountBalance.Value.ApiError.ErrorCode.ToString();

                Console.WriteLine(
                    $"Table='{TableName}', " +
                    $"Account Address='{accountBalance.Key}', " +
                    $"Account token balance='{balance}'");
            }
        }

        private static async Task ShowNFTTableExistence(
            IClient client, string tokenId)
        {
            var nftClient = new NFTClient(client);

            // CheckNFTTableAsync/CheckNFTTokenAsync return true/false —
            // no need to fetch the actual data just to know it exists.
            var tableExists = (await nftClient.CheckNFTTableAsync(TableName)).Value;
            var tokenExists =
                (await nftClient.CheckNFTTokenAsync(TableName, tokenId)).Value;
            Console.WriteLine(
                $"Table='{TableName}' exists={tableExists}, " +
                $"Token='{tokenId}' exists={tokenExists}");
        }

        private static async Task ShowNFTTable(IClient client)
        {
            var nftClient = new NFTClient(client);

            // Dump the full content of the table: every token id and owner.
            var table = (await nftClient.GetNFTTableAsync(TableName)).Value;
            foreach (var (id, owner) in table)
            {
                Console.WriteLine(
                    $"Table='{TableName}', TokenId='{id}', Owner='{owner}'");
            }
        }
    }
}
