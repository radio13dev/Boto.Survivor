using System;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NetcodeManager : MonoBehaviour
{
    public GameObject SessionUI;

    [EditorButton]
    public void ToggleHostingMode()
    {
        #if UNITY_EDITOR
        Undo.RecordObject(SessionUI, "Toggle Host mode");
        #endif
        SessionUI.SetActive(!SessionUI.activeSelf);
    }

    private void Start()
    {
        if (!SessionUI.activeSelf)
        {
            var requestedPlayType = ClientServerBootstrap.RequestedPlayType;
            
            // Server setup
            if (requestedPlayType != ClientServerBootstrap.PlayType.Client)
            {
                var ep = NetworkEndpoint.AnyIpv4.WithPort(7979);
                using var drvQuery = ClientServerBootstrap.ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.RequireConnectionApproval = false;//sceneName.Contains("ConnectionApproval", StringComparison.OrdinalIgnoreCase);
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ep);
            }

            // Client setup
            if (requestedPlayType != ClientServerBootstrap.PlayType.Server)
            {
                var ep = NetworkEndpoint.LoopbackIpv4.WithPort(7979);
                using var drvQuery = ClientServerBootstrap.ClientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(ClientServerBootstrap.ClientWorld.EntityManager, ep);
            }
        }
    }
}
