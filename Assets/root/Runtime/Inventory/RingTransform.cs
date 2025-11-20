using System;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

public class RingTransform : MonoBehaviour, HandUIController.IStateChangeListener
{
    public TransitionPoint InventoryT;
    public TransitionPoint ClosedT;
    public TransitionPoint SkillsT;
    public TransitionPoint LootT;
    public TransitionPoint MapT;
    public float AnimationTransitionTime = HandUIController.k_AnimTransitionTime;
    public ease.Mode EaseMode = ease.Mode.elastic_inout2;
    ExclusiveCoroutine Co;

    private void OnEnable()
    {
        HandUIController.Attach(this);
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        TransitionPoint target;
        switch (newState)
        {
            case HandUIController.State.Inventory:
                target = InventoryT;
                break;
            case HandUIController.State.Closed:
                target = ClosedT;
                break;
            case HandUIController.State.Skills:
                target = SkillsT;
                break;
            case HandUIController.State.Loot:
                target = LootT;
                break;
            case HandUIController.State.Map:
                target = MapT;
                break;
            default:
                throw new Exception($"Cannot transition to state {newState}");
        }

        Co.StartCoroutine(this, target.Lerp((RectTransform)transform, AnimationTransitionTime, EaseMode));
    }

    HandUIController.State m_DebugState = HandUIController.State.Closed;
    [EditorButton]
    public void GotoClosed()
    {
        foreach (var o in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (o is not HandUIController.IStateChangeListener change)
                continue;
            change.OnStateChanged(m_DebugState, HandUIController.State.Closed);
            m_DebugState = HandUIController.State.Closed;
        }
    }

    [EditorButton]
    public void GotoInventory()
    {
        foreach (var o in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (o is not HandUIController.IStateChangeListener change)
                continue;
            change.OnStateChanged(m_DebugState, HandUIController.State.Inventory);
            m_DebugState = HandUIController.State.Inventory;
        }
    }

    [EditorButton]
    public void GotoSkills()
    {
        foreach (var o in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (o is not HandUIController.IStateChangeListener change)
                continue;
            change.OnStateChanged(m_DebugState, HandUIController.State.Skills);
            m_DebugState = HandUIController.State.Skills;
        }
    }

    [EditorButton]
    public void GotoLoot()
    {
        foreach (var o in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (o is not HandUIController.IStateChangeListener change)
                continue;
            change.OnStateChanged(m_DebugState, HandUIController.State.Loot);
            m_DebugState = HandUIController.State.Loot;
        }
    }

    [EditorButton]
    public void GotoMap()
    {
        foreach (var o in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (o is not HandUIController.IStateChangeListener change)
                continue;
            change.OnStateChanged(m_DebugState, HandUIController.State.Map);
            m_DebugState = HandUIController.State.Map;
        }
    }
}