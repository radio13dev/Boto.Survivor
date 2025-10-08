using System;
using JetBrains.Annotations;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public class ChoiceUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    public static ChoiceUI Instance;

    public GameObject Container;
    public RingDisplay RingDisplay;
    public DescriptionUI DescriptionUI;
    
    public static bool IsActive => Instance && Instance.Container.activeInHierarchy;
    public static Ring ActiveRing { get; private set; }
    public static int ActiveRingIndex { get; private set; } = -1;
    public static float3 PickupPosition { get; private set; }
    public static Action<int> OnActiveRingChange;

    CompiledStats m_BaseStats;

    private void Awake()
    {
        Instance = this;
        Close();
    }

    private void OnEnable()
    {
        HandUIController.Attach(this);
    }

    private void OnDisable()
    {
        Close();
        HandUIController.Detach(this);
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        if (newState == HandUIController.State.Closed)
        {
            Close();
        }
    }
    
    public void Setup(RingDisplay grabbed)
    {
        GameEvents.TryGetComponent2<CompiledStats>(CameraTarget.MainTarget.Entity, out var stats);
        Setup(stats, grabbed.Ring, grabbed.Index);
        RingDisplay.CopyTransform(grabbed);
    }

    public void Close()
    {
        ActiveRingIndex = -1;
        OnActiveRingChange?.Invoke(ActiveRingIndex);
        
        Container.SetActive(false);
    }
    
    public void Setup(CompiledStats stats, Ring ring, int ringIndex) => _Setup(stats, ring, ringIndex, default);
    public void Setup(CompiledStats stats, Ring ring, float3 pickupPosition) => _Setup(stats, ring, -1, pickupPosition);
    private void _Setup(CompiledStats stats, Ring ring, int ringIndex, float3 pickupPosition)
    {
        ActiveRing = ring;
        ActiveRingIndex = ringIndex;
        PickupPosition = pickupPosition;
        
        RingDisplay.UpdateRing(ringIndex, ring, false);
        
        Container.SetActive(true);
        OnActiveRingChange?.Invoke(ActiveRingIndex);
        
        var desc = RingDisplay.GetDescription();
        desc.BottomLeft = string.Empty;
        for (int i = 0; i < desc.TiledStatsData?.Length; i++)
            desc.TiledStatsData[i].RingDisplayParent = null;
        desc.ButtonText = "Sell";
        desc.ButtonPress = RingDisplay.SellPress;
            
        DescriptionUI.SetText(desc);
        
    }
    
    public void SetRingFocus(bool v)
    {
        if (v) ElementFocus.SetForcedFocus(RingDisplay.GetComponent<Focusable>());
        else ElementFocus.EndForcedFocus(RingDisplay.GetComponent<Focusable>());
    }
}