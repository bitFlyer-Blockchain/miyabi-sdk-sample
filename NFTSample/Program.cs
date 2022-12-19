using Miyabi.NFT.Client;
using Miyabi.NFT.Models;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using System;
using System.Threading.Tasks;
using Utility;

namespace NFTSample
{
    class Program
    {
        const string TableName = "NFTTableSample";

        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            NFTTypesRegisterer.RegisterTypes();

            var tokenId = "my_token_id";

            await CreateNFTTable(client);
            await AddNFT(client, tokenId);
            await ShowNFTBalance(client);
            await MoveNFT(client, tokenId);
            await ShowNFTBalance(client);

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

            foreach (var address in addresses)
            {
                var result = await nftClient.GetBalanceAsync(TableName, address);
                Console.WriteLine($"address={address}, count={result.Value}");
            }
        }
    }
}
