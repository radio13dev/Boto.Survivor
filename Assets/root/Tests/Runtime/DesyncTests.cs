using System;
using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public abstract class GameTestBase
{
    static Scene m_TestScene;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        GameInitialize.EnableMainContentLoad = false;
        GameInitialize.InitMode = GameInitialize.eMode.None;
        
        if (m_TestScene == default)
            m_TestScene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/root/Runtime/Scenes/CoreScene.unity",
                new LoadSceneParameters(LoadSceneMode.Additive)
            );
    }
}

public class DesyncTester
{
    Game[] m_Games;

    public DesyncTester(params Game[] games)
    {
        m_Games = games;
    }

    public bool GetSynced()
    {
        if (m_Games.Length == 0) return true;

        bool match = true;
        GameState firstGameState = GameState.Compile(m_Games[0]);
        for (int i = 1; i < m_Games.Length; i++)
        {
            var compGameState = GameState.Compile(m_Games[i]);
            if (firstGameState.Dif(compGameState, out var error, out var mismatch)) continue;

            if (error != GameState.DifError.None)
            {
                Debug.LogError(mismatch);
                match = false;
            }
        }

        return match;
    }
}

public class DesyncTests : GameTestBase
{
    public static InputSequence[] InputSequenceValues = new[]
    {
        (InputSequence)new StepInput[]
        {
        },
        (InputSequence)new StepInput[]
        {
            new StepInput()
        },
        (InputSequence)new StepInput[]
        {
            new StepInput() { Direction = new float3(1, 1, 1) }
        }
    };

    public struct InputSequence
    {
        public StepInput[] Sequence;

        public override string ToString()
        {
            return "(" + string.Join($", ", Sequence) + ")";
        }

        public static implicit operator InputSequence(StepInput[] inputs) => new InputSequence() { Sequence = inputs };
    }

    [UnityTest]
    public IEnumerator DesyncTest_ZeroStepAdvance_Success()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        using var mockServer = GameLaunch.Create(new GameFactory("mockServer", stepProvider: stubStepProvider));
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient", stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return mockClient.JoinRelay(mockServer.Server.RelayJoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        Assert.IsTrue(mockDesyncTester.GetSynced());

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }

    [UnityTest]
    public IEnumerator DesyncTest_OneStepAdvanceBeforeConnect_Success()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        stubStepProvider.AdvanceStep(); // Advance step before connecting
        using var mockServer = GameLaunch.Create(new GameFactory("mockServer", stepProvider: stubStepProvider));
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient", stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return mockClient.JoinRelay(mockServer.Server.RelayJoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        Assert.IsTrue(mockDesyncTester.GetSynced());

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }

    [UnityTest]
    public IEnumerator DesyncTest_OneStepAdvanceAfterConnect_Success()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        using var mockServer = GameLaunch.Create(new GameFactory("mockServer", stepProvider: stubStepProvider));
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient", stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return mockClient.JoinRelay(mockServer.Server.RelayJoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        stubStepProvider.AdvanceStep(); // Advance step after connecting
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        Assert.IsTrue(mockDesyncTester.GetSynced());

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }

    [UnityTest]
    public IEnumerator DesyncTest_InputSequence_Success([ValueSource(nameof(InputSequenceValues))] InputSequence sequence)
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        using var mockServer = GameLaunch.Create(new GameFactory("mockServer", stepProvider: stubStepProvider));
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient", stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return mockClient.JoinRelay(mockServer.Server.RelayJoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        Assert.IsTrue(mockDesyncTester.GetSynced());
        for (int i = 0; i < sequence.Sequence.Length; i++)
        {
            mockServer.Server.SetStepInput(mockServer.Server.Game.PlayerIndex, sequence.Sequence[i]);
            stubStepProvider.AdvanceStep();
            yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
            Assert.IsTrue(mockDesyncTester.GetSynced());
        }

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }

    [UnityTest]
    public IEnumerator DesyncTest_InputSequenceConnectInputSequence_Success([ValueSource(nameof(InputSequenceValues))] InputSequence preConnectSequence,
        [ValueSource("InputSequenceValues")] InputSequence posConnectSequence)
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        // Advance the server to some initial pre-connect state
        using var mockServer = GameLaunch.Create(new GameFactory("mockServer", stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep);
        for (int i = 0; i < preConnectSequence.Sequence.Length; i++)
        {
            mockServer.Server.SetStepInput(mockServer.Server.Game.PlayerIndex, preConnectSequence.Sequence[i]);
            stubStepProvider.AdvanceStep();
            yield return new WaitUntil(() => mockServer.Server.WaitingForStep);
        }

        // Connect the client now
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient", stepProvider: stubStepProvider));
        yield return mockClient.JoinRelay(mockServer.Server.RelayJoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        Assert.IsTrue(mockDesyncTester.GetSynced());

        for (int i = 0; i < posConnectSequence.Sequence.Length; i++)
        {
            mockServer.Server.SetStepInput(mockServer.Server.Game.PlayerIndex, posConnectSequence.Sequence[i]);
            stubStepProvider.AdvanceStep();
            yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
            Assert.IsTrue(mockDesyncTester.GetSynced());
        }

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }

    [UnityTest]
    public IEnumerator DesyncTest_LongPlayIdle_Success()
    {
        const int PRE_CONNECT_STEPS = 1000;
        const int POST_CONNECT_STEPS = 1000;
        const int SYNC_CHECK_RATE = 100;
    
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        // Advance the server to some initial pre-connect state
        using var mockServer = GameLaunch.Create(new GameFactory("mockServer", stepProvider: stubStepProvider, showVisuals: false));
        yield return mockServer.CreateServer(); // Start server
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep);
        for (int i = 0; i < PRE_CONNECT_STEPS; i++)
            stubStepProvider.AdvanceStep();
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep);

        // Connect the client now
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient", stepProvider: stubStepProvider, showVisuals: false));
        yield return mockClient.JoinRelay(mockServer.Server.RelayJoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        Assert.IsTrue(mockDesyncTester.GetSynced());

        for (int i = 0; i < POST_CONNECT_STEPS; i++)
        {
            stubStepProvider.AdvanceStep();
            if (i % SYNC_CHECK_RATE == 0)
            {
                // Check sync every SYNC_CHECK_RATE steps
                yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
                Assert.IsTrue(mockDesyncTester.GetSynced());
                Debug.Log($"Step: {stubStepProvider.ManualStep}");
            }
        }
        
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        Assert.IsTrue(mockDesyncTester.GetSynced());

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }
}

public class StepProviderTests : GameTestBase
{
    [UnityTest]
    public IEnumerator StepProviderTests_SingleplayerZeroStepAdvance_StepZero()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        using var mockGame = GameLaunch.Create(new GameFactory("mockGame", stepProvider: stubStepProvider));
        yield return new WaitUntil(() => mockGame.InitializedAndIdle);

        // Act
        yield return new WaitUntil(() => mockGame.Singleplayer.WaitingForStep);

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockGame.Singleplayer.Game.Step);
    }

    [UnityTest]
    public IEnumerator StepProviderTests_SingleplayerOneStepAdvanceBeforeIdle_StepOne()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        stubStepProvider.AdvanceStep(); // Advance step before idle
        using var mockGame = GameLaunch.Create(new GameFactory("mockGame", stepProvider: stubStepProvider));
        yield return new WaitUntil(() => mockGame.InitializedAndIdle);

        // Act
        yield return new WaitUntil(() => mockGame.Singleplayer.WaitingForStep);

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockGame.Singleplayer.Game.Step);
    }

    [UnityTest]
    public IEnumerator StepProviderTests_SingleplayerOneStepAdvanceAfterIdle_StepOne()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        using var mockGame = GameLaunch.Create(new GameFactory("mockGame", stepProvider: stubStepProvider));
        yield return new WaitUntil(() => mockGame.InitializedAndIdle);

        // Act
        stubStepProvider.AdvanceStep(); // Advance step after idle
        yield return new WaitUntil(() => mockGame.Singleplayer.WaitingForStep);

        // Assert
        Assert.AreEqual(stubStepProvider.ManualStep, mockGame.Singleplayer.Game.Step);
    }
}

public class GameLaunchTests : GameTestBase
{
    [UnityTest]
    public IEnumerator GameLaunch_NoLobbyCode_StartSingleplayer()
    {
        // Arrange
        using var gameLaunch = GameLaunch.Create(new GameFactory("gameLaunch"));

        // Act
        yield return new WaitUntil(() => gameLaunch.InitializedAndIdle);

        // Assert
        Assert.IsTrue(gameLaunch.IsSingleplayer);
        Assert.IsFalse(gameLaunch.IsClient);
        Assert.IsFalse(gameLaunch.IsServer);
    }

    [UnityTest]
    public IEnumerator GameLaunch_LobbyCodeNoServer_ClientFailureReturnToSingleplayer()
    {
        // Arrange
        using var gameLaunch = GameLaunch.Create(new GameFactory("gameLaunch"));
        const string FAKE_LOBBY_CODE = "";

        // Act
        LogAssert.Expect(LogType.Error, ErrorMessage.JoinRelayFail);
        yield return gameLaunch.JoinRelay(FAKE_LOBBY_CODE);
        yield return new WaitUntil(() => gameLaunch.Idle);

        // Assert
        Assert.IsTrue(gameLaunch.IsSingleplayer);
        Assert.IsFalse(gameLaunch.IsClient);
        Assert.IsFalse(gameLaunch.IsServer);
    }

    [UnityTest]
    public IEnumerator GameLaunch_LobbyCodeHasServer_ClientConnect()
    {
        // Arrange
        using var stubServer = GameLaunch.Create(new GameFactory("stubServer"));
        yield return stubServer.CreateServer(); // Start server
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient"));

        // Act
        yield return mockClient.JoinRelay(stubServer.Server.RelayJoinCode); // This method should interrupt the singleplayer launch and join directly to server
        yield return new WaitUntil(() => mockClient.Idle);

        // Assert
        Assert.IsFalse(stubServer.IsSingleplayer);
        Assert.IsFalse(stubServer.IsClient);
        Assert.IsTrue(stubServer.IsServer);

        Assert.IsFalse(mockClient.IsSingleplayer);
        Assert.IsTrue(mockClient.IsClient);
        Assert.IsFalse(mockClient.IsServer);
    }

    [UnityTest]
    public IEnumerator GameTransition_SingleplayerToServer_ServerHost()
    {
        // Arrange
        using var mockGame = GameLaunch.Create(new GameFactory("mockGame"));
        yield return new WaitUntil(() => mockGame.InitializedAndIdle);

        // Act
        var oldGame = mockGame.Singleplayer.Game;
        yield return mockGame.CreateServer();
        var newGame = mockGame.Server.Game;

        // Assert
        Assert.IsFalse(mockGame.IsSingleplayer);
        Assert.IsFalse(mockGame.IsClient);
        Assert.IsTrue(mockGame.IsServer);

        Assert.AreEqual(oldGame, newGame);
    }

    [UnityTest]
    public IEnumerator GameTransition_SingleplayerToClientNoServer_ClientFailureReturnToSingleplayer()
    {
        // Arrange
        using var mockGame = GameLaunch.Create(new GameFactory("mockGame"));
        yield return new WaitUntil(() => mockGame.InitializedAndIdle);
        const string FAKE_LOBBY_CODE = "";

        // Act
        LogAssert.Expect(LogType.Error, ErrorMessage.JoinRelayFail);
        yield return mockGame.JoinRelay(FAKE_LOBBY_CODE);

        // Assert
        Assert.IsTrue(mockGame.IsSingleplayer);
        Assert.IsFalse(mockGame.IsClient);
        Assert.IsFalse(mockGame.IsServer);
    }

    [UnityTest]
    public IEnumerator GameTransition_SingleplayerToClientHasServer_ClientSuccess()
    {
        // Arrange
        using var stubServer = GameLaunch.Create(new GameFactory("stubServer"));
        yield return stubServer.CreateServer(); // Start server
        using var mockClient = GameLaunch.Create(new GameFactory("mockClient"));
        yield return new WaitUntil(() => mockClient.InitializedAndIdle); // Wait for singleplayer

        // Act
        yield return mockClient.JoinRelay(stubServer.Server.RelayJoinCode);

        // Assert
        Assert.IsFalse(stubServer.IsSingleplayer);
        Assert.IsFalse(stubServer.IsClient);
        Assert.IsTrue(stubServer.IsServer);

        Assert.IsFalse(mockClient.IsSingleplayer);
        Assert.IsTrue(mockClient.IsClient);
        Assert.IsFalse(mockClient.IsServer);
    }
}