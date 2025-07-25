using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BovineLabs.Core.Utility;
using BovineLabs.Saving;
using BovineLabs.Saving.Data;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve] 
public class ClientGameBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        Game.ClientGame = new Game(true);
        World.DefaultGameObjectInjectionWorld = Game.ClientGame.World;
        return true;
    }
}

public enum SaveState { Idle, Saving, Ready }

[Preserve]
public class Game : IDisposable
{
    public const float k_ClientPingFrequency = 1.0f / 60.0f;
    public const float k_ServerPingFrequency = 1.0f / 61.0f;
    
    public static bool ConstructorReady => SceneManager.Ready;
    
    public static Game ServerGame;
    public static Game ClientGame;
    public static Game PresentationGame => ClientGame;

    public World World => m_World;
    public int PlayerIndex = -1;

    private bool m_ShowVisuals;
    private World m_World;
    private EntityQuery m_SaveRequest;
    private EntityQuery m_SaveBuffer;
    private EntityQuery m_RenderSystemHalfTime;
    private SystemHandle m_RenderSystemGroup;
    
    private Entity m_GameManagerSceneE;
    private Entity m_GameSceneE;
    private bool m_Ready;

    public bool IsReady
    {
        get
        {
            if (m_Ready) return true;
            
            if (m_GameManagerSceneE == Entity.Null || !SceneSystem.IsSceneLoaded(m_World.Unmanaged, m_GameManagerSceneE)) return false;
            if (m_GameSceneE == Entity.Null || !SceneSystem.IsSceneLoaded(m_World.Unmanaged, m_GameSceneE)) return false;
            
            return m_Ready = true;
        }
    }

    public SaveState SaveState
    {
        get
        {
            if (!m_SaveStarted) return SaveState.Idle;
            
            if (m_SaveRequest.CalculateEntityCount() > 0)
                return SaveState.Saving;
                
            return SaveState.Ready;
        }
    }
    private bool m_SaveStarted = false;

    public void InitSave()
    {
        m_SaveStarted = true;
        m_World.EntityManager.CreateEntity(ComponentType.ReadOnly<SaveRequest>());
    }

    public void CleanSave()
    {
        m_World.EntityManager.DestroyEntity(m_SaveRequest);
        m_World.EntityManager.DestroyEntity(m_SaveBuffer);
        m_SaveStarted = false;
    }

    public Game(bool showVisuals)
    {
        Debug.Log($"Creating Game...");
        m_World = new World("Game", WorldFlags.Game);
        var systems = DefaultWorldInitialization.GetAllSystems(showVisuals ? WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Presentation : WorldSystemFilterFlags.ServerSimulation).ToList();
        systems.Remove(typeof(UpdateWorldTimeSystem));
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(m_World, systems);
        
        var saveRequest = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SaveRequest),
            },
            Options = EntityQueryOptions.Default
        };
        m_SaveRequest = m_World.EntityManager.CreateEntityQuery(saveRequest);
        var saveBuffer = new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SaveBuffer),
            },
            Options = EntityQueryOptions.Default
        };
        m_SaveBuffer = m_World.EntityManager.CreateEntityQuery(saveBuffer);
        
        m_ShowVisuals = showVisuals;
        if (m_ShowVisuals)
        {
            m_RenderSystemHalfTime = m_World.EntityManager.CreateEntityQuery(new ComponentType(typeof(RenderSystemHalfTime)));
            m_RenderSystemGroup = m_World.GetExistingSystem<RenderSystemGroup>();
        }
    }
    
    public void LoadScenes()
    {
        Debug.Log($"Loading subscene with GUID: {SceneManager.GameManagerScene.SceneGUID}");
        m_GameManagerSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SceneManager.GameManagerScene.SceneGUID);
            
        Debug.Log($"Loading subscene with GUID: {SceneManager.GameScene.SceneGUID}");
        m_GameSceneE = SceneSystem.LoadSceneAsync(m_World.Unmanaged, SceneManager.GameScene.SceneGUID);
    }

    public void Dispose()
    {
        m_World.Dispose();
    }
    
    public unsafe void SendSave(ref DataStreamWriter writer)
    {
        EntityManager entityManager = m_World.EntityManager;
        var saveBufferEntities = m_SaveBuffer.ToEntityArray(Allocator.Temp);
        if (saveBufferEntities.Length > 0)
        {
            Debug.Log("Multiple save buffers found, weird.");
        }
        else if (saveBufferEntities.Length == 0)
        {
            Debug.LogError($"No save buffer found, failed send.");
            return;
        }
        
        var saveBuffer = entityManager.GetBuffer<SaveBuffer>(saveBufferEntities[0]);
        var reint = saveBuffer.Reinterpret<byte>();
        writer.WriteInt(reint.Length);
        writer.WriteBytes(reint.AsNativeArray());
        Debug.Log($"... wrote {reint.Length}...");
    }

    public unsafe void LoadSave(NativeArray<byte> ptr)
    {
        EntityManager entityManager = m_World.EntityManager;
        var loadBufferE = entityManager.CreateEntity(ComponentType.ReadWrite<LoadBuffer>());
        var loadBuffer = entityManager.GetBuffer<LoadBuffer>(loadBufferE);
        loadBuffer.AddRange(ptr.Reinterpret<LoadBuffer>());
        m_Ready = false; // Unset ready.
    }

    public unsafe void ApplyStepData(FullStepData stepData, SpecialLockstepActions* extraActionPtr)
    {
        var entityManager = m_World.EntityManager;

        // Iterate step count
        {
            var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepController)));
            var stepController = query.GetSingleton<StepController>();
            
            if (stepData.Step == -1)
                stepData.Step = stepController.Step + 1;

            if (stepData.Step != stepController.Step + 1)
            {
                Debug.LogError($"Failed step, tried to go from step {stepController.Step} to {stepData.Step}");
                return;
            }

            entityManager.SetComponentData(query.GetSingletonEntity(), new StepController(stepData.Step));
        }
        
        // Setup time for the frame
        m_World.SetTime(new TimeData(stepData.Step*(double)Game.k_ClientPingFrequency, Game.k_ClientPingFrequency));
        if (m_ShowVisuals) m_RenderSystemHalfTime.SetSingleton(new RenderSystemHalfTime(){ Value = 0 });
        
        // Apply extra actions
        {
            if (stepData.ExtraActionCount > 0)
            {
                Debug.Log($"Step {stepData.Step}: Applying {stepData.ExtraActionCount} extra actions");
                for (byte i = 0; i < stepData.ExtraActionCount; i++)
                    extraActionPtr[i].Apply(m_World);
            }
        }
        
        // Apply inputs
        {
            var query = entityManager.CreateEntityQuery(new ComponentType(typeof(StepInput)), new ComponentType(typeof(PlayerControlled)));
            var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length > 0)
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var index = entityManager.GetComponentData<PlayerControlled>(entities[i]).Index;
                    var data = stepData[index];
                    entityManager.SetComponentData(entities[i], data);
                }
            }

            entities.Dispose();
        }
        
        
        m_World.Update();
    }

    public void ApplyRender(float t)
    {
        m_RenderSystemHalfTime.SetSingleton(new RenderSystemHalfTime(){ Value = t });
        m_RenderSystemGroup.Update(m_World.Unmanaged);
    }
}

[Save]
public struct PlayerControlled : IComponentData
{
    public int Index;
}