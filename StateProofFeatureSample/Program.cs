using System;
using System.Threading.Tasks;
using Miyabi.Asset.Models;
using Miyabi.Binary.Models;
using Miyabi.ClientSdk;
using Miyabi.ClientSdk.Client;
using Miyabi.Common.Models;
using Miyabi.Entity.Models;
using Miyabi.NFT.Models;
using Miyabi.PrivateData.Models;
using Utility;

namespace StateProofFeatureSample;

class Program
{
    static async Task Main(string[] args)
    {
        // Set up the miyabi client.
        var config = new SdkConfig(
            [Utils.ApiUrl],
            privateChannel: new PrivateChannel(Utils.PdoMembers));

        var handler =
            Utils.GetBypassRemoteCertificateValidationHandler();
        var client = new Client(config, handler);

        // In order to use a miyabi modules, registering types is required.
        AssetTypesRegisterer.RegisterTypes();
        BinaryTypesRegisterer.RegisterTypes();
        EntityTypesRegisterer.RegisterTypes();
        NFTTypesRegisterer.RegisterTypes();
        PrivateDataTypesRegisterer.RegisterTypes();

        // Asset table: Add data, get & verify the State-proof
        await AssetStateProofGetAndVerify(client);

        // Binary table: Add data, get & verify the State-proof
        await BinaryStateProofGetAndVerify(client);

        // NFT table: Add data, get & verify the State-proof
        await NftStateProofGetAndVerify(client);

        // Entity table: Add data, get & verify the State-proof
        await EntityStateProofGetAndVerify(client);

        // Private data table: Add data, get & verify the State-proof
        await PrivateDataStateProofGetAndVerify(client);

        // State table: Add data, get & verify the State-proof
        await SystemDataStateProofGetAndVerify(client);

        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
    }

    static async Task AssetStateProofGetAndVerify(IClient client)
    {
        Utils.GetConsoleBanner(
            '*',
            $" {nameof(AssetStateProofGetAndVerify)} Begin ");

        await AssetTableStateProof.CreateAssetTableAndData(client);
        var (atHeight, stateProof) =
            await AssetTableStateProof.GetAssetStateProof(
                client,
                AssetTableStateProof.UserAddress);
        var assetData =
            await AssetTableStateProof.GetAssetData(
                client,
                AssetTableStateProof.UserAddress);
        await AssetTableStateProof.VerifyAssetStateProof(
            client,
            atHeight - 1,
            stateProof,
            AssetTableStateProof.UserAddress,
            assetData);

        Utils.GetConsoleBanner(
            '*',
            $"{ nameof(AssetStateProofGetAndVerify)} End ");

    }

    static async Task BinaryStateProofGetAndVerify(IClient client)
    {
        Utils.GetConsoleBanner(
            '*',
            $" {nameof(BinaryStateProofGetAndVerify)} Begin ");

        await BinaryTableStateProof.CreateBinaryTableAndData(client);
        var (atHeight, stateProof) =
            await BinaryTableStateProof.GetBinaryStateProof(
                client,
                BinaryTableStateProof.Key);
        var binaryData =
            await BinaryTableStateProof.GetBinaryData(
                client,
                BinaryTableStateProof.Key);
        await BinaryTableStateProof.VerifyBinaryStateProof(
            client,
            atHeight - 1,
            stateProof,
            BinaryTableStateProof.Key,
            binaryData);

        Utils.GetConsoleBanner(
            '*',
            $"{nameof(BinaryStateProofGetAndVerify)} End");

    }

    static async Task NftStateProofGetAndVerify(IClient client)
    {
        Utils.GetConsoleBanner(
            '*',
            $" {nameof(NftStateProofGetAndVerify)} Begin ");

        await NftTableStateProof.CreateNftTableAndData(client);
        var (atHeight, stateProof) =
            await NftTableStateProof.GetNftStateProof(
                client,
                NftTableStateProof.TokenId);
        var nftData =
            await NftTableStateProof.GetNftData(
                client,
                NftTableStateProof.TokenId);
        await NftTableStateProof.VerifyNftStateProof(
            client,
            atHeight - 1,
            stateProof,
            NftTableStateProof.TokenId,
            nftData);

        Utils.GetConsoleBanner(
            '*',
            $" {nameof(NftStateProofGetAndVerify)} End ");
    }

    static async Task EntityStateProofGetAndVerify(IClient client)
    {
        Utils.GetConsoleBanner(
            '*',
            $" {nameof(EntityStateProofGetAndVerify)} Begin ");

        await EntityTableStateProof.CreateEntityTableAndData(client);
        var (atHeight, stateProof) =
            await EntityTableStateProof.GetEntityStateProof(
                client,
                EntityTableStateProof.Key);
        var entityData =
            await EntityTableStateProof.GetEntityData(
                client,
                EntityTableStateProof.Key);
        await EntityTableStateProof.VerifyEntityStateProof(
            client,
            atHeight - 1,
            stateProof,
            EntityTableStateProof.Key,
            entityData);

        Utils.GetConsoleBanner(
            '*',
            $" {nameof(EntityStateProofGetAndVerify)} End ");

    }

    static async Task PrivateDataStateProofGetAndVerify(IClient client)
    {
        Utils.GetConsoleBanner(
            '*',
            $" {nameof(PrivateDataStateProofGetAndVerify)} Begin ");

        await PrivateDataTableStateProof.CreatePrivateDataTableAndData(client);
        var (atHeight, stateProof) =
            await PrivateDataTableStateProof.GetPrivateDataStateProof(
                client,
                PrivateDataTableStateProof.RawKey);
        var privateDataEntryData =
            await PrivateDataTableStateProof.GetPrivateDataEntryData(
                client,
                PrivateDataTableStateProof.RawKey);
        await PrivateDataTableStateProof.VerifyPrivateDataStateProof(
            client,
            atHeight - 1,
            stateProof,
            PrivateDataTableStateProof.RawKey,
            privateDataEntryData);

        Utils.GetConsoleBanner(
            '*',
            $" {nameof(PrivateDataStateProofGetAndVerify)} End ");
    }

    static async Task SystemDataStateProofGetAndVerify(IClient client)
    {
        Utils.GetConsoleBanner(
            '*',
            $" {nameof(SystemDataStateProofGetAndVerify)} Begin ");

        var (atHeight, stateProof) =
            await SystemTableStateProof.GetSystemTableStateProof(client);
        int data =
            await SystemTableStateProof.GetCurrentHeight(client);
        await SystemTableStateProof.VerifySystemTableStateProof(
            client,
            atHeight - 1,
            stateProof,
            data);

        Utils.GetConsoleBanner(
            '*',
            $" {nameof(SystemDataStateProofGetAndVerify)} End ");
    }
}