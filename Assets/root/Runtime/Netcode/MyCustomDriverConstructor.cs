using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.TLS;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

public struct MyCustomDriverConstructor : INetworkStreamDriverConstructor
{
    public bool connectWithSsl;
    public string hostname;

    private const int DefaultPayloadCapacity = 16 * 1024;
    private const int DefaultWindowSize = 32;
    private const int MaxSimPacketCount = 64;

    public void CreateClientDriver(World world, ref NetworkDriverStore store, NetDebug netDebug)
    {
        NetworkSettings settings = new NetworkSettings();
        NetworkConfigParameter networkConfigSettings = settings.GetNetworkConfigParameters();

        if (connectWithSsl)
        {
            Debug.Log("Connecting securly");
            settings.WithSecureClientParameters(hostname);
        }

        settings.WithReliableStageParameters(DefaultWindowSize)
            .WithFragmentationStageParameters(DefaultPayloadCapacity);

#if UNITY_EDITOR
        settings.WithSimulatorStageParameters(MaxSimPacketCount);
        settings.WithNetworkSimulatorParameters();
#endif
        DefaultDriverBuilder.RegisterClientWebSocketDriver(world, ref store, netDebug, settings);
    }

    public string serverCertificate;
    public string serverPrivateKey;

    public void CreateServerDriver(World world, ref NetworkDriverStore store, NetDebug netDebug)
    {
        NetworkSettings settings = new NetworkSettings();

        if (!string.IsNullOrEmpty(serverCertificate) && !string.IsNullOrEmpty(serverPrivateKey))
        {
        
            settings.WithSecureServerParameters(
                certificate: serverCertificate,
                privateKey: serverPrivateKey);
        
            Debug.Log("Starting secure server");
        }
        else
        {
            Debug.Log("Starting unsecure server");
        }
        settings.WithReliableStageParameters(DefaultWindowSize)
            .WithFragmentationStageParameters(DefaultPayloadCapacity);

#if UNITY_EDITOR
        settings.WithSimulatorStageParameters(MaxSimPacketCount);
        settings.WithNetworkSimulatorParameters();
#endif

#if !UNITY_WEBGL || UNITY_EDITOR
        DefaultDriverBuilder.RegisterServerWebSocketDriver (world, ref store, netDebug, settings);
#endif
    }
}