using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public abstract class GameTestBase
{
    [OneTimeSetUp]
    public void OneTimeSetup()
        => EditorSceneManager.LoadSceneInPlayMode(
            "Assets/root/Tests/Runtime/TestScene.unity",
            new LoadSceneParameters(LoadSceneMode.Additive)
        );
}

public class DesyncTester
{
    public DesyncTester(params Game[] games)
    {
        
    }
    
    public bool GetSynced() => true;
}

public class DesyncTests : GameTestBase
{
    [UnityTest]
    public IEnumerator DesyncTest_ZeroStepAdvance_Success()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        var mockServer = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        var mockClient = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return mockClient.JoinLobby(mockServer.Server.JoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        bool areSynced = mockDesyncTester.GetSynced();

        // Assert
        Assert.IsTrue(areSynced);
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }
    [UnityTest]
    public IEnumerator DesyncTest_OneStepAdvanceBeforeConnect_Success()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        stubStepProvider.AdvanceStep(); // Advance step before connecting
        var mockServer = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        var mockClient = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return mockClient.JoinLobby(mockServer.Server.JoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        bool areSynced = mockDesyncTester.GetSynced();

        // Assert
        Assert.IsTrue(areSynced);
        Assert.AreEqual(stubStepProvider.ManualStep, mockServer.Server.Game.Step);
        Assert.AreEqual(stubStepProvider.ManualStep, mockClient.Client.Game.Step);
    }
    [UnityTest]
    public IEnumerator DesyncTest_OneStepAdvanceAfterConnect_Success()
    {
        // Arrange
        var stubStepProvider = new ManualStepProvider();
        var mockServer = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        var mockClient = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        yield return mockServer.CreateServer(); // Start server
        yield return mockClient.JoinLobby(mockServer.Server.JoinCode);
        var mockDesyncTester = new DesyncTester(mockServer.Server.Game, mockClient.Client.Game);

        // Act
        stubStepProvider.AdvanceStep(); // Advance step after connecting
        yield return new WaitUntil(() => mockServer.Server.WaitingForStep && mockClient.Client.WaitingForStep);
        bool areSynced = mockDesyncTester.GetSynced();

        // Assert
        Assert.IsTrue(areSynced);
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
        var mockGame = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        yield return new WaitUntil(() => mockGame.Idle);

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
        var mockGame = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        yield return new WaitUntil(() => mockGame.Idle);

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
        var mockGame = GameLaunch.Create(new GameFactory(stepProvider: stubStepProvider));
        yield return new WaitUntil(() => mockGame.Idle);

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
        var gameLaunch = GameLaunch.Create(new GameFactory());

        // Act
        yield return new WaitUntil(() => gameLaunch.Initialized && gameLaunch.Idle);

        // Assert
        Assert.IsTrue(gameLaunch.IsSingleplayer);
        Assert.IsFalse(gameLaunch.IsClient);
        Assert.IsFalse(gameLaunch.IsServer);
    }

    [UnityTest]
    public IEnumerator GameLaunch_LobbyCodeNoServer_ClientFailureReturnToSingleplayer()
    {
        // Arrange
        var gameLaunch = GameLaunch.Create(new GameFactory());
        const string FAKE_LOBBY_CODE = "";

        // Act
        LogAssert.Expect(LogType.Error, ErrorMessage.JoinRelayFail);
        yield return gameLaunch.JoinLobby(FAKE_LOBBY_CODE);
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
        var stubServer = GameLaunch.Create(new GameFactory());
        yield return stubServer.CreateServer(); // Start server
        var mockClient = GameLaunch.Create(new GameFactory());

        // Act
        yield return mockClient.JoinLobby(stubServer.Server.JoinCode); // This method should interrupt the singleplayer launch and join directly to server
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
        var mockGame = GameLaunch.Create(new GameFactory());
        yield return new WaitUntil(() => mockGame.Idle);

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
        var mockGame = GameLaunch.Create(new GameFactory());
        yield return new WaitUntil(() => mockGame.Idle);
        const string FAKE_LOBBY_CODE = "";

        // Act
        LogAssert.Expect(LogType.Error, ErrorMessage.JoinRelayFail);
        yield return mockGame.JoinLobby(FAKE_LOBBY_CODE);

        // Assert
        Assert.IsTrue(mockGame.IsSingleplayer);
        Assert.IsFalse(mockGame.IsClient);
        Assert.IsFalse(mockGame.IsServer);
    }

    [UnityTest]
    public IEnumerator GameTransition_SingleplayerToClientHasServer_ClientSuccess()
    {
        // Arrange
        var stubServer = GameLaunch.Create(new GameFactory());
        yield return stubServer.CreateServer(); // Start server
        var mockClient = GameLaunch.Create(new GameFactory());
        yield return new WaitUntil(() => mockClient.Idle); // Wait for singleplayer

        // Act
        yield return mockClient.JoinLobby(stubServer.Server.JoinCode);

        // Assert
        Assert.IsFalse(stubServer.IsSingleplayer);
        Assert.IsFalse(stubServer.IsClient);
        Assert.IsTrue(stubServer.IsServer);

        Assert.IsFalse(mockClient.IsSingleplayer);
        Assert.IsTrue(mockClient.IsClient);
        Assert.IsFalse(mockClient.IsServer);
    }
}