using UnityEngine;
using UnityEngine.Events;

public class ToggleOnDeath : MonoBehaviour
{
    [SerializeField] private UnityEvent _onLocalPlayerDeath;
        
    private void Awake()
    {
        GameEvents.OnEvent += OnGemEvent;
    }

    private void OnDestroy()
    {
        GameEvents.OnEvent -= OnGemEvent;
    }

    private void OnGemEvent(GameEvents.Data data)
    {
        var eType = data.Type; var entity = data.Entity;
        if (eType != GameEvents.Type.PlayerDied) return;
        if (!GameEvents.TryGetSharedComponent<PlayerControlled>(entity, out var player)) return;
        if (player.Index != Game.ClientGame.PlayerIndex) return;
        _onLocalPlayerDeath?.Invoke();
    }
}