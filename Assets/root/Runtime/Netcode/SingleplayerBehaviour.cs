using System;
using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

public unsafe class SingleplayerBehaviour : MonoBehaviour
{
    public bool TickEnabled = true;

    private float m_T;
    private bool m_InitComplete;
    private NativeArray<SpecialLockstepActions> m_SpecialActionArr;
    private Game m_Game;

    private IEnumerator Start()
    {
        if (Game.SingleplayerGame != null) Debug.LogError($"Running multiple singleplayer games at the same time...");
        
        m_SpecialActionArr = new NativeArray<SpecialLockstepActions>(4, Allocator.Persistent);
        
        // Build game
        yield return new WaitUntil(() => Game.ConstructorReady);
        m_Game = new Game(true);
        m_Game.LoadScenes();
        yield return new WaitUntil(() =>
        {
            m_Game.World.Update();
            return m_Game.IsReady;
        });
        
        m_Game.RunGameWorldInit();
        
        // Spawn the player
        m_Game.PlayerIndex = 0;
        SpawnPlayer();
        ApplyStep();
        
        // Complete
        m_InitComplete = true;
        Game.SingleplayerGame = m_Game;
    }

    private void OnDestroy()
    {
        m_SpecialActionArr.Dispose();
    }

    FullStepData m_StepData;
    StepInput m_Inputs;
    private void Update()
    {
        if (!m_InitComplete) return;
        
        m_Inputs.Collect();
        
        if (TickEnabled && (m_T += Time.deltaTime) >= Game.k_ClientPingFrequency)
        {
            m_T -= Game.k_ClientPingFrequency;
            ApplyStep();
            m_Inputs = default;
        }
        else
        {
            m_Game.ApplyRender(m_T/Game.k_ClientPingFrequency);
        }
    }

    [EditorButton]
    private void ApplyStep()
    {
        m_StepData = new FullStepData(m_StepData.Step + 1, m_Inputs)
        {
            ExtraActionCount = m_StepData.ExtraActionCount
        };
        m_Game.ApplyStepData(m_StepData, (SpecialLockstepActions*)m_SpecialActionArr.GetUnsafePtr());
        m_StepData.ExtraActionCount = 0;
    }

    [EditorButton]
    public void SpawnPlayer()
    {
        m_SpecialActionArr[m_StepData.ExtraActionCount] = SpecialLockstepActions.PlayerJoin(0);
        m_StepData.ExtraActionCount++;
    }
}