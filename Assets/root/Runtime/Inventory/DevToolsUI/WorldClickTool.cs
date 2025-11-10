using System;
using UnityEngine;
using UnityEngine.UI;

public class WorldClickTool : MonoBehaviour
{
    public GameObject Visual;
    public ClickMode Mode;
    public bool Active;

    public enum ClickMode
    {
        PlaceDummy,
        PlaceEnemy,
        PlaceEnemyLarge,
        PlaceGem,
        PlaceEnemyWheel,
        PlaceEnemyTrap,
        PlaceRing,
        KillBind
    }

    private void Awake()
    {
        if (TryGetComponent<Toggle>(out var toggle))
            SetActive(toggle.isOn);
    }

    private void OnDisable()
    {
        if (TryGetComponent<Toggle>(out var toggle))
            toggle.isOn = false;
    }

    public void SetActive(bool active)
    {
        Active = active;
        if (!active) Visual.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!Active) return;

        if (GameInput.Inputs.UI.RightClick.WasPressedThisFrame())
        {
            if (TryGetComponent<Toggle>(out var toggle))
                toggle.isOn = false;
            else
                SetActive(false);
            return;
        }

        Visual.gameObject.SetActive(TorusCollider.IsMouseOver);
        if (!TorusCollider.IsMouseOver) return;

        Visual.transform.SetPositionAndRotation(TorusCollider.LastRaycast.pointerCurrentRaycast.worldPosition,
            Quaternion.LookRotation(Camera.main.transform.up, TorusCollider.LastRaycast.pointerCurrentRaycast.worldNormal));

        if (GameInput.Inputs.UI.Click.WasPressedThisFrame())
            switch (Mode)
            {
                case ClickMode.PlaceDummy:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminPlaceEnemy((byte)Game.ClientGame.PlayerIndex, Visual.transform.position, 0, GameRpc.EnemySpawnOptions.Stationary | GameRpc.EnemySpawnOptions.NoAi | GameRpc.EnemySpawnOptions.InfiniteHealth));
                    break;
                case ClickMode.PlaceEnemy:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminPlaceEnemy((byte)Game.ClientGame.PlayerIndex, Visual.transform.position, 0, default));
                    break;
                case ClickMode.PlaceEnemyLarge:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminPlaceEnemy((byte)Game.ClientGame.PlayerIndex, Visual.transform.position, 1, default));
                    break;
                case ClickMode.PlaceEnemyWheel:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminPlaceEnemy((byte)Game.ClientGame.PlayerIndex, Visual.transform.position, 2, default));
                    break;
                case ClickMode.PlaceEnemyTrap:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminPlaceEnemy((byte)Game.ClientGame.PlayerIndex, Visual.transform.position, 3, default));
                    break;
                case ClickMode.PlaceGem:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminPlaceGem((byte)Game.ClientGame.PlayerIndex, Visual.transform.position));
                    break;
                case ClickMode.PlaceRing:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminPlaceRing((byte)Game.ClientGame.PlayerIndex, Visual.transform.position));
                    break;
                case ClickMode.KillBind:
                    Game.ClientGame.RpcSendBuffer.Enqueue(GameRpc.AdminKillBind((byte)Game.ClientGame.PlayerIndex));
                    break;
            }
    }
}