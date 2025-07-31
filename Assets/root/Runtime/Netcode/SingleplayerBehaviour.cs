using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public unsafe class SingleplayerBehaviour : GameHostBehaviour
{
    public bool TickEnabled = true;

    private float m_T;
    private bool m_InitComplete;
    private NativeArray<SpecialLockstepActions> m_SpecialActionArr;
    public Game m_Game;

    private IEnumerator Start()
    {
        // Build game
        yield return new WaitUntil(() => Game.ConstructorReady);

        m_SpecialActionArr = new NativeArray<SpecialLockstepActions>(PingClientBehaviour.k_MaxSpecialActionCount, Allocator.Persistent);
        m_Game.LoadScenes();
        yield return new WaitUntil(() =>
        {
            if (m_Game.IsReady) return true;
            m_Game.World.Update();
            return false;
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
        m_Game?.Dispose();
        m_SpecialActionArr.Dispose();
    }

    FullStepData m_StepData;
    StepInput m_Inputs;

    private void Update()
    {
        if (!m_InitComplete) return;

        m_Inputs.Collect(Camera.main);

        if (TickEnabled && (m_T += Time.deltaTime) >= Game.k_ClientPingFrequency)
        {
            m_T -= Game.k_ClientPingFrequency;
            m_T = math.min(m_T, Game.k_ClientPingFrequency);
            ApplyStep();
            m_Inputs = default;
        }
        else
        {
            m_Game.ApplyRender(m_T / Game.k_ClientPingFrequency);
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

        m_StepData = new FullStepData(m_Game.Step + 1)
        {
            ExtraActionCount = m_StepData.ExtraActionCount
        };
        m_StepData[m_Game.PlayerIndex] = m_Inputs;
        m_Game.ApplyStepData(m_T / Game.k_ClientPingFrequency, m_StepData, (SpecialLockstepActions*)m_SpecialActionArr.GetUnsafePtr());
        m_StepData.ExtraActionCount = 0;
    }

    [EditorButton]
    public void Host()
    {
        var server = gameObject.AddComponent<PingServerBehaviour>();
        server.m_Game = m_Game;
        server.StartCoroutine(server.Connect(
            OnSuccess: () =>
            {
                server.AddLocalPlayer();
                m_Game = null; // Null out our game so we don't destroy the server when we destroy this component
                Destroy(this);
                Game.ServerGame = server.m_Game;
            },
            OnFailure: () =>
            {
                Debug.LogError($"Failed to start server");
                server.m_Game = null;
                Destroy(server);
            })
        );
    }
}