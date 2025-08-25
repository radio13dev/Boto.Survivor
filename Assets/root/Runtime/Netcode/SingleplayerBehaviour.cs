using System;
using System.Collections;
using BovineLabs.Core.Extensions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public unsafe class SingleplayerBehaviour : GameHostBehaviour
{
    private bool m_InitComplete;
    private NativeArray<GameRpc> m_SpecialActionArr;
    public override bool Idle => m_InitComplete && Game != null && Game.IsReady;

    private IEnumerator Start()
    {
        // Build game
        yield return new WaitUntil(() => Game.ConstructorReady);

        m_SpecialActionArr = new NativeArray<GameRpc>(PingClientBehaviour.k_MaxSpecialActionCount, Allocator.Persistent);
        Game.LoadScenes();
        yield return new WaitUntil(() =>
        {
            if (Game.IsReady) return true;
            Game.Update_NoLogic();
            return false;
        });

        // Randomise seed
        Game.InitWorld();
        
        // Complete
        m_InitComplete = true;

        // Spawn the player
        if (Game.PlayerIndex == -1)
        {
            Game.PlayerIndex = 0;
            Game.RpcSendBuffer.Enqueue(GameRpc.PlayerJoin(0, 0));
        }
    }

    private void OnDestroy()
    {
        Game?.Dispose();
        m_SpecialActionArr.Dispose();
    }

    FullStepData m_StepData;
    StepInput m_Inputs;

    private void Update()
    {
        if (!m_InitComplete) return;

        m_Inputs.Collect(Camera.main);

        if (Game.CanStep())
        {
            // Load rpcs
            while (m_StepData.ExtraActionCount < PingClientBehaviour.k_MaxSpecialActionCount && Game.RpcSendBuffer.TryDequeue(out var specialAction))
            {
                m_SpecialActionArr[m_StepData.ExtraActionCount] = specialAction;
                m_StepData.ExtraActionCount++;
            }

            m_StepData = new FullStepData(Game.Step + 1)
            {
                ExtraActionCount = m_StepData.ExtraActionCount
            };
            m_StepData[Game.PlayerIndex] = m_Inputs;
            Game.ApplyStepData(m_StepData, (GameRpc*)m_SpecialActionArr.GetUnsafePtr());
            m_StepData.ExtraActionCount = 0;
            m_Inputs = default;
        }
        else
        {
            Game.ApplyRender();
        }
    }
}