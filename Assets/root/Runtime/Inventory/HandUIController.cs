using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HandUIController : MonoBehaviour
{
    public enum State { Closed, Neutral, Inventory, Map }
    public static event Action<State, State> OnStateChanged;
    static State m_State = State.Closed;
    
    public const float k_AnimTransitionTime = 0.2f;
    
    public interface IStateChangeListener
    {
        void OnStateChanged(State oldState, State newState);
    }
    
    public static void SetState(HandUIController.State state)
    {
        var oldState = m_State;
        m_State = state;
        
        if (oldState != m_State || m_State == State.Closed)
            OnStateChanged?.Invoke(oldState, m_State);
    }

    public static void Attach(IStateChangeListener listener)
    {
        OnStateChanged += listener.OnStateChanged;
        listener.OnStateChanged(State.Closed, m_State);
    }

    public static void Detach(IStateChangeListener listener)
    {
        OnStateChanged -= listener.OnStateChanged;
    }

    private void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
            if (m_State == State.Closed) SetState(State.Neutral);
            else SetState(State.Closed);
    }
}

public static class SelectableExtensions
{
    public static void Deselect(this Selectable selectable)
    {
        if (selectable && UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == selectable.gameObject)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }
}