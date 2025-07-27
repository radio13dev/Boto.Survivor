using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
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
        if (Game.ClientGame != null && Game.ClientGame.IsReady)
        {
            Debug.LogError($"Running multiple singleplayer games at the same time, creating new one...");
            Game.ClientGame = new Game(true);
            World.DefaultGameObjectInjectionWorld = Game.ClientGame.World;
        }
        
        // Build game
        yield return new WaitUntil(() => Game.ConstructorReady && Game.ClientGame != null);
        m_Game = Game.ClientGame;
        
        m_SpecialActionArr = new NativeArray<SpecialLockstepActions>(PingClientBehaviour.k_MaxSpecialActionCount, Allocator.Persistent);
        m_Game.LoadScenes();
        yield return new WaitUntil(() =>
        {
            m_Game.World.Update();
            return m_Game.IsReady;
        });
        
        // Spawn the player
        m_Game.PlayerIndex = 0;
        m_Game.RpcSendBuffer.Enqueue(SpecialLockstepActions.PlayerJoin(0));
        ApplyStep();
        
        // Complete
        m_InitComplete = true;
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
            m_T = math.min(m_T, Game.k_ClientPingFrequency);
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
        // Load rpcs
        while (m_StepData.ExtraActionCount < PingClientBehaviour.k_MaxSpecialActionCount && m_Game.RpcSendBuffer.TryDequeue(out var specialAction))
        {
            m_SpecialActionArr[m_StepData.ExtraActionCount] = specialAction;
            m_StepData.ExtraActionCount++;
        }
        
        m_StepData = new FullStepData(m_StepData.Step + 1, m_Inputs)
        {
            ExtraActionCount = m_StepData.ExtraActionCount
        };
        m_Game.ApplyStepData(m_StepData, (SpecialLockstepActions*)m_SpecialActionArr.GetUnsafePtr());
        m_StepData.ExtraActionCount = 0;
    }
}