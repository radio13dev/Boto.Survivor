using System;
using System.Collections.ObjectModel;
using UnityEngine;

public class RingDisplay : MonoBehaviour
{
    public MeshRenderer NoRingDisplay;
    public MeshRenderer HasRingDisplay;

    Ring m_Ring;
    EquippedGem[] m_Gems;
    public ReadOnlyCollection<EquippedGem> Gems => Array.AsReadOnly(m_Gems);

    private void Awake()
    {
        UpdateRing(default, ReadOnlySpan<EquippedGem>.Empty);
    }

    public void UpdateRing(Ring ring, ReadOnlySpan<EquippedGem> equippedGemsForRing)
    {
        m_Ring = ring;
        m_Gems = equippedGemsForRing.ToArray();
        
        NoRingDisplay.gameObject.SetActive(!ring.Stats.IsValid);
        HasRingDisplay.gameObject.SetActive(ring.Stats.IsValid);
    }
}