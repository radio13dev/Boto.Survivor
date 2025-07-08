using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.HelloNetcode
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class HelloNetcodeSystemGroup : ComponentSystemGroup
    {}

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation, WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class HelloNetcodeInputSystemGroup : ComponentSystemGroup
    {}

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class HelloNetcodePredictedSystemGroup : ComponentSystemGroup
    {}
}
