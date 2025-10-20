using BovineLabs.Saving;
using Drawing;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Collider = Collisions.Collider;

public class LobbyZoneAuthoring : MonoBehaviourGizmos
{
    partial class Baker : Baker<LobbyZoneAuthoring>
    {
        public override void Bake(LobbyZoneAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.WorldSpace);
            AddComponent(entity, new LobbyZone()
            {
                Owner = byte.MaxValue,
                IsPrivate = false,
                IsReady = false
            });
            AddBuffer<PrivateLobbyWhitelist>(entity);
            AddComponent(entity, Collider.Sphere(LobbyZone.ColliderRadius));
        }
    }

    public override void DrawGizmos()
    {
        using (Draw.WithLineWidth(2, false))
        {
            Draw.WireSphere(transform.position, LobbyZone.ColliderRadius, new Color(0.5f, 0f, 0.3f, 0.5f));
        }
    }
}
/// <summary>
/// Tag for lobbies
/// </summary>
[Save]
public struct LobbyZone : IComponentData
{
    public const float ColliderRadius = 5;

    public byte Owner;
    public bool IsPrivate;
    public bool IsReady;
    
    public static LobbyZone ClientsideLobby => new LobbyZone(){ Owner = byte.MaxValue }; 
}

[Save]
public struct PrivateLobbyWhitelist : IBufferElementData
{
    public byte Player;
}

/// <summary>
/// Closes lobbies that are empty
/// </summary>
[GameTypeOnlySystem(0)]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[UpdateAfter(typeof(DestroySystemGroup))]
public partial struct LobbyCollisionSystem : ISystem
{
    EntityQuery m_PlayerQuery;

    public void OnCreate(ref SystemState state)
    {
        m_PlayerQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Circling, PlayerControlled, PlayerControlledSaveable>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Delete lobbies that have 0 players inside them
        var playerTransforms = m_PlayerQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
        var playerIds = m_PlayerQuery.ToComponentDataArray<PlayerControlledSaveable>(Allocator.TempJob);
        var circlings = m_PlayerQuery.ToComponentDataArray<Circling>(Allocator.TempJob);
        state.Dependency = new Job()
        {
            PlayerTransforms = playerTransforms,
            PlayerIds = playerIds,
            PlayerCirclings = circlings,
            DestroyEmptyLobbies = false, // TODO: Check if game mode is singleplayer lobby. Singleplayer lobbies are created and destroyed locally.
        }.Schedule(state.Dependency);
        playerTransforms.Dispose(state.Dependency);
        playerIds.Dispose(state.Dependency);
        circlings.Dispose(state.Dependency);
    }
    
    [WithPresent(typeof(DestroyFlag))]
    partial struct Job : IJobEntity
    {
        public EntityCommandBuffer ecb;
        [ReadOnly] public NativeArray<PlayerControlledSaveable> PlayerIds;
        [ReadOnly] public NativeArray<LocalTransform> PlayerTransforms;
        [ReadOnly] public NativeArray<Circling> PlayerCirclings;
        [ReadOnly] public bool DestroyEmptyLobbies;
    
        public void Execute(Entity lobbyE, ref LobbyZone lobbyZone, in DynamicBuffer<PrivateLobbyWhitelist> lobbyWhitelist, in LocalTransform lobbyT, in Collider lobbyC, EnabledRefRW<DestroyFlag> destroyFlag)
        {
            bool alive = false;
            bool allCharged = true;
            
            var c = lobbyC.Apply(lobbyT);
            for (int i = 0; i < PlayerTransforms.Length; i++)
            {
                if (lobbyZone.IsPrivate)
                {
                    bool allowed = false;
                    for (int j = 0; j < lobbyWhitelist.Length; j++)
                    {
                        if (lobbyWhitelist[j].Player == PlayerIds[i].Index)
                        {
                            allowed = true;
                            break;
                        }
                    }
                    if (!allowed) continue;
                }
                if (c.Overlaps(Collider.DefaultAABB(1).Apply(PlayerTransforms[i])))
                {
                    alive = true;
                    
                    // Check if everyone inside has charged.
                    allCharged &= PlayerCirclings[i].Percentage >= 1f;
                }
            }
            
            if (!alive)
            {
                if (DestroyEmptyLobbies) destroyFlag.ValueRW = true;
                return;
            }
            
            lobbyZone.IsReady = allCharged;
            // TODO: Serverside only: Create lobby here.
        }
    }
}

/// <summary>
/// Provides static references to lobby nearest the player character
/// </summary>
[GameTypeOnlySystem(0)]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
[UpdateAfter(typeof(LobbyCollisionSystem))]
public partial class LobbyPresentationSystem : SystemBase
{
    public static Entity CurrentLobbyEntity = Entity.Null;
    public static LobbyZone CurrentLobby = default;
    public static float3 CurrentLobbyPos = float3.zero;
    
    EntityQuery m_LobbyQuery;
    EntityQuery m_PlayerQuery;

    protected override void OnCreate()
    {
        base.OnCreate();
        
        m_LobbyQuery = SystemAPI.QueryBuilder().WithAll<LobbyZone, LocalTransform, Collider>().Build();
        m_PlayerQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, PlayerControlled>().Build();
    }

    protected override void OnUpdate()
    {
        if (Game.ClientGame == null) return;
        
        m_PlayerQuery.SetSharedComponentFilter(new PlayerControlled(){Index = Game.ClientGame.PlayerIndex});
        if (m_PlayerQuery.IsEmpty)
        {
            CurrentLobbyEntity = Entity.Null;
            return;
        }
        
        var playerE = m_PlayerQuery.GetSingletonEntity();
        var playerT = m_PlayerQuery.GetSingleton<LocalTransform>();
        var playerC = SystemAPI.GetComponent<Collider>(playerE).Apply(playerT);
        
        using var lobbyEntities = m_LobbyQuery.ToEntityArray(Allocator.Temp);
        using var lobbies = m_LobbyQuery.ToComponentDataArray<LobbyZone>(Allocator.Temp);
        using var lobbyTransforms = m_LobbyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var lobbyColliders = m_LobbyQuery.ToComponentDataArray<Collider>(Allocator.Temp);
        
        LobbyZone closestLobby = default;
        Entity closestLobbyEntity = Entity.Null;
        float closestLobbyDist = float.MaxValue;
        float3 closestLobbyPos = float3.zero;
        for (int i = 0; i < lobbyTransforms.Length; i++)
        {
            if (lobbies[i].IsPrivate)
            {
                bool allowed = false;
                var whitelist = SystemAPI.GetBuffer<PrivateLobbyWhitelist>(lobbyEntities[i]);
                for (int j = 0; j < whitelist.Length; j++)
                {
                    if (whitelist[j].Player == Game.ClientGame.PlayerIndex)
                    {
                        allowed = true;
                        break;
                    }
                }
                if (!allowed) continue;
            }
        
            var c = lobbyColliders[i].Apply(lobbyTransforms[i]);
            if (c.Overlaps(playerC))
            {
                var d = math.distance(playerT.Position, lobbyTransforms[i].Position);
                if (d < closestLobbyDist)
                {
                    closestLobby = lobbies[i];
                    closestLobbyDist = d;
                    closestLobbyEntity = lobbyEntities[i];
                    closestLobbyPos = lobbyTransforms[i].Position;
                }
            }
        }
        
        CurrentLobbyEntity = closestLobbyEntity;
        CurrentLobbyPos = closestLobbyPos;
        CurrentLobby = closestLobby;
    }
}

/// <summary>
/// Provides static references to nearest player character for interaction
/// </summary>
[GameTypeOnlySystem(0)]
[UpdateInGroup(typeof(SurvivorSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
public partial class LobbyPlayerInteractionSystem : SystemBase
{
    public static Entity NearestPlayer = Entity.Null;
    public static float3 NearestPlayerPos = float3.zero;
    
    EntityQuery m_NearbyPlayerQuery;
    EntityQuery m_PlayerQuery;

    protected override void OnCreate()
    {
        base.OnCreate();
        
        m_NearbyPlayerQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, PlayerControlled, Collider>().Build();
        m_PlayerQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, PlayerControlled>().Build();
    }

    protected override void OnUpdate()
    {
        if (Game.ClientGame == null)
            return;
        
        m_PlayerQuery.SetSharedComponentFilter(new PlayerControlled(){Index = Game.ClientGame.PlayerIndex});
        if (m_PlayerQuery.IsEmpty)
        {
            NearestPlayer = Entity.Null;
            return;
        }
        
        var playerE = m_PlayerQuery.GetSingletonEntity();
        var playerT = m_PlayerQuery.GetSingleton<LocalTransform>();
        var playerC = SystemAPI.GetComponent<Collider>(playerE).Apply(playerT);
        
        using var playerEntities = m_NearbyPlayerQuery.ToEntityArray(Allocator.Temp);
        using var playerTransforms = m_NearbyPlayerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var playerColliders = m_NearbyPlayerQuery.ToComponentDataArray<Collider>(Allocator.Temp);
        
        Entity closestPlayer = Entity.Null;
        float closestPlayerDist = float.MaxValue;
        float3 closestPlayerPos = float3.zero;
        for (int i = 0; i < playerTransforms.Length; i++)
        {
            if (playerEntities[i] == playerE) continue;
        
            var c = playerColliders[i].Apply(playerTransforms[i]);
            if (c.Overlaps(playerC))
            {
                var d = math.distance(playerT.Position, playerTransforms[i].Position);
                if (d < closestPlayerDist)
                {
                    closestPlayerDist = d;
                    closestPlayer = playerEntities[i];
                    closestPlayerPos = playerTransforms[i].Position;
                }
            }
        }
        
        NearestPlayer = closestPlayer;
        NearestPlayerPos = closestPlayerPos;
    }
}