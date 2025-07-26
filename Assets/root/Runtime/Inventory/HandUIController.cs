using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HandUIController : MonoBehaviour
{
    public static Selectable LastPressed
    {
        get
        {
            return s_LastPressed;
        }
        set
        {
            var old = s_LastPressed;
            s_LastPressed = value;
            OnLastPressChanged?.Invoke(old, s_LastPressed);
        }
    }
    static Selectable s_LastPressed;

    static event Action<State, State> OnStateChanged;
    static event Action<Selectable, Selectable> OnLastPressChanged;
    
    static State m_State = State.Closed;
    public enum State { Closed, Neutral, Inventory, Map }
    
    public const float k_AnimTransitionTime = 0.2f;
    
    public interface IStateChangeListener
    {
        void OnStateChanged(State oldState, State newState);
    }
    public interface ILastPressListener
    {
        void OnLastPressChanged(Selectable oldPressed, Selectable newPressed);
    }
    
    public static void SetState(HandUIController.State state)
    {
        var oldState = m_State;
        m_State = state;
        
        if (oldState != m_State || m_State == State.Closed)
            OnStateChanged?.Invoke(oldState, m_State);
    }

    public static void Attach(object listener)
    {
        if (listener is IStateChangeListener stateChangeListener) 
        {
            OnStateChanged += stateChangeListener.OnStateChanged;
            stateChangeListener.OnStateChanged(State.Closed, m_State);
        }
        if (listener is ILastPressListener pressListener)
        {
            OnLastPressChanged += pressListener.OnLastPressChanged;
            pressListener.OnLastPressChanged(null, LastPressed);
        }
    }

    public static void Detach(object listener)
    {
        if (listener is IStateChangeListener stateChangeListener) 
        {
            OnStateChanged -= stateChangeListener.OnStateChanged;
        }
        if (listener is ILastPressListener pressListener)
        {
            OnLastPressChanged -= pressListener.OnLastPressChanged;
        }
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