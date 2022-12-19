using Miyabi;
using Miyabi.Asset.Models;
using Miyabi.Binary.Models;
using Miyabi.ClientSdk;
using Miyabi.Contract.Models;
using Miyabi.Entity.Models;
using System;
using System.Threading.Tasks;
using Miyabi.NFT.Models;
using Miyabi.PrivateData.Models;
using Utility;

namespace GeneralApiCall
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var handler = Utils.GetBypassRemoteCertificateValidationHandler();

            var config = new SdkConfig(Utils.ApiUrl);
            var client = new Client(config, handler);

            // In order to use a miyabi module, registering types is required.
            CommonTypesRegisterer.RegisterCommonTypes();
            AssetTypesRegisterer.RegisterTypes();
            BinaryTypesRegisterer.RegisterTypes();
            EntityTypesRegisterer.RegisterTypes();
            ContractTypesRegisterer.RegisterTypes();
            NFTTypesRegisterer.RegisterTypes();
            PrivateDataTypesRegisterer.RegisterTypes();
            ContractRegistration.Initialize();

            await GetTables(client);
        }

        private static async Task GetTables(IClient client)
        {
            // General API has SendTransactionAsync
            var generalApi = new GeneralApi(client);
            var result = await generalApi.GetTablesAsync();
            foreach(var item in result.Value)
            {
                Console.WriteLine($"table name={item.Name}");
            }
        }

    }
}
