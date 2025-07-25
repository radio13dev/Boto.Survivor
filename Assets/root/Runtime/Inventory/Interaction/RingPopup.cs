using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

public class RingPopup : MonoBehaviour
{
    public GameObject InteractionNotification;
    public GameObject HeldHighlight;
    RingEquipTransaction m_Transaction;
    
    public void Focus(Entity nearestE)
    {
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (m_Transaction == null || m_Transaction.InteractedEntity != nearestE)
            {
                // Start transaction
                m_Transaction = new();
                FingerUI.Transaction = m_Transaction;
                FingerUI.Instances[m_Transaction.HoveredInventoryIndex].Select();
            }
            else
            {
                // Complete transaction
                m_Transaction.Apply();
                m_Transaction = null;
            }
        }
    }

    private void OnDisable()
    {
        m_Transaction = null;
    }
}

public class RingEquipTransaction
{
    public Entity InteractedEntity;
    public int HoveredInventoryIndex;

    public void Apply()
    {
        Game.PresentationGame.RpcSendBuffer.Enqueue(SpecialLockstepActions.Rpc_PlayerAdjustInventory(Game.PresentationGame.PlayerIndex, byte.MaxValue, HoveredInventoryIndex));
    }
}