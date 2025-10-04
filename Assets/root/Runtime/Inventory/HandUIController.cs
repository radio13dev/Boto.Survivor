using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HandUIController : MonoBehaviour
{
    public static Selectable LastPressed
    {
        get { return s_LastPressed; }
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

    static LinkedList<IStateSource> m_StateSources = new();
    static State m_StateCore = State.Closed;
    static State m_OldState = State.Closed;

    public enum State
    {
        Closed,
        Inventory,
        Map
    }

    public interface IStateSource
    {
        State GetUIState();
    }

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
        m_StateCore = state;
        _RefreshState();
    }

    public static void AddStateLayer(IStateSource stateSource)
    {
        if (m_StateSources.Contains(stateSource)) return;
        m_StateSources.AddLast(stateSource);
        _RefreshState();
    }

    public static void RemoveStateLayer(IStateSource stateSource)
    {
        m_StateSources.Remove(stateSource);
        _RefreshState();
    }

    private static void _RefreshState()
    {
        State current = GetState();

        if (m_OldState != current || current == State.Closed)
        {
            var old = m_OldState;
            m_OldState = old;
            OnStateChanged?.Invoke(old, current);
        }
    }

    public static void Attach(object listener)
    {
        if (listener is IStateChangeListener stateChangeListener)
        {
            OnStateChanged += stateChangeListener.OnStateChanged;
            stateChangeListener.OnStateChanged(State.Closed, m_OldState);
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
        if (GameInput.Inputs.UI.InventoryShortcut.WasPressedThisFrame())
            if (m_StateCore == State.Inventory) SetState(State.Closed);
            else SetState(State.Inventory);
        if (GameInput.Inputs.UI.MapShortcut.WasPressedThisFrame())
            if (m_StateCore == State.Map) SetState(State.Closed);
            else SetState(State.Map);
    }

    public static State GetState()
    {
        State current = m_StateCore;
        if (m_StateCore == State.Closed && m_StateSources.Count > 0)
            current = m_StateSources.Last.Value.GetUIState();
        return current;
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