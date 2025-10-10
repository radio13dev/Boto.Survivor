using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[Save]
public readonly struct Pending : IBufferElementData
{
    [Save]
    public readonly struct Dirty : IComponentData, IEnableableComponent{}
    public enum eType : byte
    {
        Damage,
        // Chain,
        Cut,
        Degenerate,
        Subdivide,
        Decimate,
        Dissolve,
        Poke,
    }
    
    public readonly eType Type;
    public readonly int Value;

    private Pending(eType type, int value)
    {
        Type = type;
        Value = value;
    }

    public static Pending Damage(int value) => new Pending(eType.Damage, value);
    
    public static Pending Cut(byte value) => new Pending(eType.Cut, value);
    public static Pending Degenerate(byte value) => new Pending(eType.Degenerate, value);
    public static Pending Subdivide(byte value) => new Pending(eType.Subdivide, value);
    public static Pending Decimate(byte value) => new Pending(eType.Decimate, value);
    public static Pending Dissolve(byte value) => new Pending(eType.Dissolve, value);
    public static Pending Poke(byte value) => new Pending(eType.Poke, value);
}

[UpdateInGroup(typeof(ProjectileDamageSystemGroup))]
public partial struct PendingSystem : ISystem
{
    bool m_IsPresentation;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SharedRandom>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        m_IsPresentation = state.WorldUnmanaged.SystemExists<LightweightRenderSystem>();
        Debug.Log($"Is presentation: {m_IsPresentation}");
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter presentationEcb;
        Entity cutVisual;
        Entity degenerateVisual;
        Entity subdivideVisual;
        Entity decimateVisual;
        Entity dissolveVisual;
        
        Entity cutDamageVisual;
        Entity pokeDamageVisual;
        
        Entity decimateProjectileTemplate;
        
        if (m_IsPresentation)
        {
            presentationEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var prefabs = SystemAPI.GetSingletonBuffer<GameManager.Prefabs>(true);
            cutVisual = prefabs[GameManager.Prefabs.Status_cutVisual].Entity;
            degenerateVisual = prefabs[GameManager.Prefabs.Status_degenerateVisual].Entity;
            subdivideVisual = prefabs[GameManager.Prefabs.Status_subdivideVisual].Entity;
            decimateVisual = prefabs[GameManager.Prefabs.Status_decimateVisual].Entity;
            dissolveVisual = prefabs[GameManager.Prefabs.Status_dissolveVisual].Entity;
            
            cutDamageVisual = prefabs[GameManager.Prefabs.Status_cutDamageVisual].Entity;
            pokeDamageVisual = prefabs[GameManager.Prefabs.Status_pokeDamageVisual].Entity;
            
            decimateProjectileTemplate = prefabs[GameManager.Prefabs.PlayerProjectile_Decimate].Entity;
        }
        else
        {
            presentationEcb = default;
            cutVisual = default;
            degenerateVisual = default;
            subdivideVisual = default;
            decimateVisual = default;
            dissolveVisual = default;
            
            cutDamageVisual = default;
            pokeDamageVisual = default;
            
            decimateProjectileTemplate = default;
        }
        
        new Job()
        {
            Time = SystemAPI.Time.ElapsedTime,
            IsPresentation = m_IsPresentation,
            ecb_presentation = presentationEcb,
            SharedRandom = SystemAPI.GetSingleton<SharedRandom>(),
            cutVisual = cutVisual,
            degenerateVisual = degenerateVisual,
            subdivideVisual = subdivideVisual,
            decimateVisual = decimateVisual,
            dissolveVisual = dissolveVisual,
            
            cutDamageVisual = cutDamageVisual,
            pokeDamageVisual = pokeDamageVisual,
            
            decimateProjectileTemplate = decimateProjectileTemplate,
        }.Schedule();
    }

    [WithAll(typeof(Pending.Dirty))]
    [WithPresent(typeof(Cut),typeof(Degenerate),typeof(Subdivide),typeof(Decimate),typeof(Dissolve), typeof(Poke))]
    partial struct Job : IJobEntity
    {
        [ReadOnly] public double Time;
        
        // ~~ Presentation Only ~~
        [ReadOnly] public bool IsPresentation;
        public EntityCommandBuffer.ParallelWriter ecb_presentation;
        [ReadOnly] public SharedRandom SharedRandom;
        [ReadOnly] public Entity cutVisual;
        [ReadOnly] public Entity degenerateVisual;
        [ReadOnly] public Entity subdivideVisual;
        [ReadOnly] public Entity decimateVisual;
        [ReadOnly] public Entity dissolveVisual;
        
        [ReadOnly] public Entity cutDamageVisual;
        [ReadOnly] public Entity pokeDamageVisual;
        
        [ReadOnly] public Entity decimateProjectileTemplate;
        // ~~~~~~~~~~~~~~~~~~~~~~~
    
        public void Execute([ChunkIndexInQuery] int Key, Entity e, in LocalTransform t, ref Health health,
        
            ref Cut cut,
            ref Degenerate degenerate,
            ref Subdivide subdivide,
            ref Subdivide.Timer subdivideTimer,
            ref Decimate decimate,
            ref Dissolve dissolve,
            
            EnabledRefRW<Cut> cutState,
            EnabledRefRW<Degenerate> degenerateState,
            EnabledRefRW<Subdivide> subdivideState,
            EnabledRefRW<Decimate> decimateState,
            EnabledRefRW<Dissolve> dissolveState,
            
            ref DynamicBuffer<Pending> pending,
            EnabledRefRW<Pending.Dirty> pendingDirty)
        {
            var random_presentation = SharedRandom.Get(e.Index);
            
            int decimations = 0;
            
            // Apply effects effect application
            for (int i = 0; i < pending.Length; i++)
            {
                switch (pending[i].Type)
                {
                    case Pending.eType.Cut:
                        cut.Value += (byte)pending[i].Value;
                        cutState.ValueRW = cut;
                        SetupParticle(ref ecb_presentation, in Key, ref random_presentation, in cutVisual, in t, Time, 0.5f);
                        break;
                    case Pending.eType.Degenerate:
                        degenerate.Value += (byte)pending[i].Value;
                        degenerateState.ValueRW = degenerate;
                        SetupParticle(ref ecb_presentation, in Key, ref random_presentation, in degenerateVisual, in t, Time, 0.5f);
                        break;
                    case Pending.eType.Subdivide:
                        if (!subdivide) subdivideTimer.TriggerTime = Time + Subdivide.Timer.Duration;
                        subdivide.Value += (byte)pending[i].Value;
                        subdivideState.ValueRW = subdivide;
                        SetupParticle(ref ecb_presentation, in Key, ref random_presentation, in subdivideVisual, in t, Time, 0.5f);
                        break;
                    case Pending.eType.Decimate:
                        const byte decimate_cap = 100;
                        int decimate_v = decimate.Value + pending[i].Value;
                        if (decimate_v >= 100)
                        {
                            int procs = decimate_v / decimate_cap;
                            decimate_v -= procs * decimate_cap;
                            decimations += procs;
                        }
                        decimate.Value = (byte)decimate_v;
                        decimateState.ValueRW = decimate;
                        SetupParticle(ref ecb_presentation, in Key, ref random_presentation, in decimateVisual, in t, Time, 0.5f);
                        break;
                    case Pending.eType.Dissolve:
                        dissolve.Value += (byte)pending[i].Value;
                        dissolveState.ValueRW = dissolve;
                        SetupParticle(ref ecb_presentation, in Key, ref random_presentation, in dissolveVisual, in t, Time, 0.5f);
                        break;
                }
            }
            
            // Create decimation projectiles (we do this here using a SEPARATE random to avoid sync issues)
            for (int i = 0; i < decimations; i++)
                Decimate.Setup(ref ecb_presentation, Key, decimateProjectileTemplate, SharedRandom.Get(i), t, Time);
            
            // Apply damage
            float dmgMul = degenerate.Multiplier;
            for (int i = 0; i < pending.Length; i++)
            {
                switch (pending[i].Type)
                {
                    case Pending.eType.Damage:
                        int dmg = (int)math.ceil(pending[i].Value*dmgMul);
                        health.Value -= dmg;
                        GameEvents.Trigger(GameEvents.Type.EnemyHealthChanged, e, -dmg);
                        
                        if (cut)
                        {
                            int dmgCut = (int)math.ceil(cut.Value*dmgMul);
                            health.Value -= dmgCut;
                            GameEvents.Trigger(GameEvents.Type.EnemyHealthChanged, e, -dmgCut);
                            SetupParticle(ref ecb_presentation, in Key, ref random_presentation, in cutDamageVisual, in t, Time, 0.5f);
                        }
                        break;
                    
                    case Pending.eType.Poke:
                        int dmgPoke = (int)math.ceil(pending[i].Value*dmgMul);
                        health.Value -= dmgPoke;
                        GameEvents.Trigger(GameEvents.Type.EnemyHealthChanged, e, -dmgPoke);
                        SetupParticle(ref ecb_presentation, in Key, ref random_presentation, in pokeDamageVisual, in t, Time, 0.5f);
                        break;
                }
            }
            
            pendingDirty.ValueRW = false;
            pending.Clear();
        }

        private void SetupParticle(ref EntityCommandBuffer.ParallelWriter ecbPresentation, in int key, ref Random random, in Entity particleTemplate, in LocalTransform transform, in double time, in double life)
        {
            if (!IsPresentation) return;
            
            var particle = ecbPresentation.Instantiate(key, particleTemplate);
            var adjusted = transform.Translate(random.NextFloat3Direction());
            ecbPresentation.SetComponent(key, particle, adjusted);
            ecbPresentation.SetComponent(key, particle, new SpawnTimeCreated(Time));
            ecbPresentation.SetComponent(key, particle, new DestroyAtTime(Time + life));
        }
    }
}