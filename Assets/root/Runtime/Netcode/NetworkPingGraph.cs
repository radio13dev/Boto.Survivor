using System;
using System.Collections.Generic;
using TMPro;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public static partial class NetworkPing
{
    public static readonly SharedStatic<NativeList<(DateTime, int)>> ServerPingTimes = SharedStatic<NativeList<(DateTime, int)>>.GetOrCreate<ServerPingTimesKey>();
    private class ServerPingTimesKey {}
    public static readonly SharedStatic<NativeList<(DateTime, int)>> ClientPingTimes = SharedStatic<NativeList<(DateTime, int)>>.GetOrCreate<ClientPingTimesKey>();
    private class ClientPingTimesKey {}
    public static readonly SharedStatic<NativeList<(DateTime, int)>> ClientExecuteTimes = SharedStatic<NativeList<(DateTime, int)>>.GetOrCreate<ClientExecuteTimesKey>();
    private class ClientExecuteTimesKey {}
    
    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        if (ServerPingTimes.Data.IsCreated) ServerPingTimes.Data.Dispose();
        ServerPingTimes.Data = new NativeList<(DateTime, int)>(Allocator.Persistent);
        
        if (ClientPingTimes.Data.IsCreated) ClientPingTimes.Data.Dispose();
        ClientPingTimes.Data = new NativeList<(DateTime, int)>(Allocator.Persistent);
        
        if (ClientExecuteTimes.Data.IsCreated) ClientExecuteTimes.Data.Dispose();
        ClientExecuteTimes.Data = new NativeList<(DateTime, int)>(Allocator.Persistent);
    }
}

public class NetworkPingGraph : MonoBehaviour
{
    public float DisplayedSeconds = 10;
    public Graph ServerTimeGraph;
    public TMP_Text ServerStep;
    public Graph ClientPingTimeGraph;
    public TMP_Text ClientPingStep;
    public Graph ClientExecuteTimeGraph;
    public TMP_Text ClientExecuteStep;

    private void Update()
    {
        long min = long.MaxValue;
        var curTime = DateTime.Now;
        {
            // Calc min
            using (var it = NetworkPing.ServerPingTimes.Data.AsReadOnly().GetEnumerator())
                while (it.MoveNext())
                    min = math.min(min, it.Current.Item2);
                    
            List<float2> serverTimes = new();
            using (var it = NetworkPing.ServerPingTimes.Data.AsReadOnly().GetEnumerator())
                while (it.MoveNext())
                    serverTimes.Add(new float2((float)(it.Current.Item1 - curTime).TotalSeconds, it.Current.Item2 - min));
            ServerTimeGraph.Show(new Graph.DataSet(serverTimes), -DisplayedSeconds, 1f, 0, DisplayedSeconds*60, true);
            
            if (serverTimes.Count > 0) 
                ServerStep.text = serverTimes[^1].y.ToString("N0");
                
            while (NetworkPing.ServerPingTimes.Data.Length > 0 && NetworkPing.ServerPingTimes.Data[0].Item1 < curTime.Add(TimeSpan.FromSeconds(-DisplayedSeconds)))
                NetworkPing.ServerPingTimes.Data.RemoveAt(0);
        }
        {
            List<float2> ClientPingTimes = new();
            using (var it = NetworkPing.ClientPingTimes.Data.AsReadOnly().GetEnumerator())
                while (it.MoveNext())
                    ClientPingTimes.Add(new float2((float)(it.Current.Item1 - curTime).TotalSeconds, it.Current.Item2 - min));
            ClientPingTimeGraph.Show(new Graph.DataSet(ClientPingTimes), -DisplayedSeconds, 1f, 0, DisplayedSeconds*60, true);
            
            if (ClientPingTimes.Count > 0) 
                ClientPingStep.text = ClientPingTimes[^1].y.ToString("N0");
                
            while (NetworkPing.ClientPingTimes.Data.Length > 0 && NetworkPing.ClientPingTimes.Data[0].Item1 < curTime.Add(TimeSpan.FromSeconds(-DisplayedSeconds)))
                NetworkPing.ClientPingTimes.Data.RemoveAt(0);
        }
        {
            List<float2> ClientExecuteTimes = new();
            using (var it = NetworkPing.ClientExecuteTimes.Data.AsReadOnly().GetEnumerator())
                while (it.MoveNext())
                    ClientExecuteTimes.Add(new float2((float)(it.Current.Item1 - curTime).TotalSeconds, it.Current.Item2 - min));
            ClientExecuteTimeGraph.Show(new Graph.DataSet(ClientExecuteTimes), -DisplayedSeconds, 1f, 0, DisplayedSeconds*60, true);
            
            if (ClientExecuteTimes.Count > 0) 
                ClientExecuteStep.text = ClientExecuteTimes[^1].y.ToString("N0");
            
            while (NetworkPing.ClientExecuteTimes.Data.Length > 0 && NetworkPing.ClientExecuteTimes.Data[0].Item1 < curTime.Add(TimeSpan.FromSeconds(-DisplayedSeconds)))
                NetworkPing.ClientExecuteTimes.Data.RemoveAt(0);
        }
    }
}
