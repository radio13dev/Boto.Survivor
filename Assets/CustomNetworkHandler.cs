using System.Threading.Tasks;
using Unity.Multiplayer.Widgets;
using Unity.Services.Multiplayer;
using UnityEngine;

public class CustomNetworkHandler : CustomWidgetsNetworkHandler
{
    public override Task StartAsync(NetworkConfiguration configuration)
    {
        throw new System.NotImplementedException();
    }

    public override Task StopAsync()
    {
        throw new System.NotImplementedException();
    }
}
