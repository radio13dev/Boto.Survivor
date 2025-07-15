using System;
using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public unsafe class SingleplayerBehaviour : MonoBehaviour
{
    public float T;
    public bool InitComplete;
    private NativeArray<SpecialLockstepActions> m_SpecialActionArr;
    private Game m_Game;

    private IEnumerator Start()
    {
        if (Game.SingleplayerGame != null) Debug.LogError($"Running multiple singleplayer games at the same time...");
        
        m_SpecialActionArr = new NativeArray<SpecialLockstepActions>(4, Allocator.Persistent);
        
        // Build game
        yield return new WaitUntil(() => Game.ConstructorReady);
        m_Game = new Game(true);
        yield return new WaitUntil(() =>
        {
            m_Game.World.Update();
            return m_Game.IsReady;
        });
        
        // Spawn the player
        m_Game.PlayerIndex = 0;
        SpawnPlayer();
        ApplyStep();
        
        // Complete
        InitComplete = true;
        Game.SingleplayerGame = m_Game;
    }

    private void OnDestroy()
    {
        m_SpecialActionArr.Dispose();
        m_Game?.Dispose();
    }

    FullStepData m_StepData;
    StepInput m_Inputs;
    private void Update()
    {
        if (!InitComplete) return;
        
        m_Inputs.Collect();
        T += Time.deltaTime;
        if (T >= Game.k_ClientPingFrequency)
        {
            T -= Game.k_ClientPingFrequency;
            var renderSystem = m_Game.World.Unmanaged.GetExistingUnmanagedSystem<LightweightRenderSystem>();
            m_Game.World.Unmanaged.GetUnsafeSystemRef<LightweightRenderSystem>(renderSystem).t = 0;
            ApplyStep();
            m_Inputs = default;
        }
        else
        {
            var renderSystem = m_Game.World.Unmanaged.GetExistingUnmanagedSystem<LightweightRenderSystem>();
            m_Game.World.Unmanaged.GetUnsafeSystemRef<LightweightRenderSystem>(renderSystem).t = T/Game.k_ClientPingFrequency;
            renderSystem.Update(m_Game.World.Unmanaged);
        }
    }

    [EditorButton]
    private void ApplyStep()
    {
        m_StepData = new FullStepData(m_StepData.Step + 1, m_Inputs)
        {
            ExtraActionCount = m_StepData.ExtraActionCount
        };
        m_StepData.Apply(m_Game.World, (SpecialLockstepActions*)m_SpecialActionArr.GetUnsafePtr());
        m_StepData.ExtraActionCount = 0;
    }

    [EditorButton]
    public void SpawnPlayer()
    {
        m_SpecialActionArr[m_StepData.ExtraActionCount] = SpecialLockstepActions.PlayerJoin(0);
        m_StepData.ExtraActionCount++;
    }
}