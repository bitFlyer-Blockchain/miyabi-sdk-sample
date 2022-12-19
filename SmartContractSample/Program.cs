using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using Miyabi.Contract.Client;
using Miyabi.Contract.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Utility;

/// <summary>
/// Program.cs deploys, instantiates, invokes and queries a smart contracts.
/// The smart contract it self is sc/sc1.cs
/// </summary>
namespace SmartContractSample
{
    class Program
    {
        const string ContractName = "Contract.Sample.SC1"; // Name Space + "." + Class name
        const string InstanceName = "SmartContractInstanceSample";

        static readonly ByteString s_AssemblyId =
            ContractUtils.GetAssemblyId(new[] { File.ReadAllText("sc\\sc1.cs") });

        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            ContractTypesRegisterer.RegisterTypes();

            await DeployContract(client);
            await InstantiateContract(client);
            await InvokeContract(client);
            await QueryMethod(client, "Read", new [] { "01" });

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        private static async Task DeployContract(IClient client)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);

            // Create entry
            var sources = new[] { File.ReadAllText("sc\\sc1.cs") };
            var instantiators = new[] { new PublicKeyAddress(Utils.GetOwnerKeyPair().PublicKey) };
            var entry = new ContractDeploy(sources, instantiators: instantiators);

            // Create transaction
            var tx = TransactionCreator.CreateTransaction(
                new[] { entry },
                new[] { new SignatureCredential(Utils.GetContractAdminKeyPair().PublicKey) });

            // Sign transaction. To deploy a smart contract, contract admin private key is
            // required
            var txSigned = TransactionCreator.SignTransaction(
                tx,
                new[] { Utils.GetContractAdminKeyPair().PrivateKey });

            // Send transaction
            await generalApi.SendTransactionAsync(txSigned);

            // Wait until the transaction is stored in a block and get the result
            var result = await Utils.WaitTx(generalApi, tx.Id);
            Console.WriteLine($"txid={tx.Id}, result={result}");
        }

        private static async Task InstantiateContract(IClient client)
        {
            var generalApi = new GeneralApi(client);

            // Create gen entry
            var arguments = new[] { "dummy" };
            var owners = new Address[] {new PublicKeyAddress(Utils.GetOwnerKeyPair())};
            var entry = new ContractInstantiate(s_AssemblyId, ContractName, InstanceName, owners, arguments);

            // Create signed transaction with builder. To generate instantiate contract,
            // table admin and contract owner private key is required.
            var txSigned = TransactionCreator.CreateTransactionBuilder(
                new [] { entry },
                new []
                {
                    new SignatureCredential(Utils.GetTableAdminKeyPair().PublicKey),
                    new SignatureCredential(Utils.GetOwnerKeyPair().PublicKey)
                })
                .Sign(Utils.GetTableAdminKeyPair().PrivateKey)
                .Sign(Utils.GetOwnerKeyPair().PrivateKey)
                .Build();

            await generalApi.SendTransactionAsync(txSigned);

            var result = await Utils.WaitTx(generalApi, txSigned.Id);
            Console.WriteLine($"txid={txSigned.Id}, result={result}");
        }

        private static async Task InvokeContract(IClient client)
        {
            var generalApi = new GeneralApi(client);

            // Create gen entry
            var entry = new ContractInvoke(s_AssemblyId, ContractName, InstanceName, "Write", new[] { "01", "02" });

            // Create signed transaction with builder. To invoke a smart contract,
            // contract owner's private key is required.
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

        private static async Task QueryMethod(IClient client, string method, string[] arguments)
        {
            // ContractClient has access to asset endpoints
            var contractClient = new ContractClient(client);

            var result = await contractClient.QueryContractAsync(s_AssemblyId, ContractName, InstanceName, method, arguments);
            Console.WriteLine($"value={result.Value}");
        }
    }
}
