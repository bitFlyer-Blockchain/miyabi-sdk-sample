using Miyabi;
using Miyabi.ClientSdk;
using Miyabi.Common.Models;
using Miyabi.Cryptography;
using System.Net.Http;
using System.Threading.Tasks;

namespace Utility
{
    public class Utils
    {
        // Change API url according to miyabi config.json
        public static string ApiUrl = "https://localhost:9010";

        // Change table admin private key according to miyabi blockchain config
        public static KeyPair GetTableAdminKeyPair() =>
            GetKeyPair("10425b7e6ebf5e0d5918717f77ce8a66aaf92bc64b65996f885ff12bd94ef529");

        // Change contract admin private key according to miyabi blockchain config
        public static KeyPair GetContractAdminKeyPair() =>
            GetKeyPair("14e3a2d16c8a43a4eb1b088b32bca2abaf274e3f185afc9c15b33491c8deb9a6");

        // Represetns table owner or contract owner
        public static KeyPair GetOwnerKeyPair() =>
            GetKeyPair("0000000000000000000000000000000000000000000000000000000000000001");

        // Represetns users
        public static KeyPair GetUser0KeyPair() =>
            GetKeyPair("0000000000000000000000000000000000000000000000000000000000000010");

        public static KeyPair GetUser1KeyPair() =>
            GetKeyPair("0000000000000000000000000000000000000000000000000000000000000020");

        public static KeyPair GetKeyPair(string privateKey)
        {
            var adminPrivateKey =
                PrivateKey.Parse(privateKey);
            return new KeyPair(adminPrivateKey);
        }

        public static async Task<string> WaitTx(GeneralApi api, ByteString id)
        {
            while (true)
            {
                var result = await api.GetTransactionResultAsync(id);
                if (result.Value.ResultCode != TransactionResultCode.Pending)
                {
                    return result.Value.ResultCode.ToString();
                }
            }
        }

        public static HttpClientHandler GetBypassRemoteCertificateValidationHandler()
        {
            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) => true,
            };

            return handler;
        }
    }
}
