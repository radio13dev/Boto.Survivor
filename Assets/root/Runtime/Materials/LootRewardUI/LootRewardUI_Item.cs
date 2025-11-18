using UnityEngine;

public class LootRewardUI_Item : MonoBehaviour, DescriptionUI.ISource
{
    public RingDisplay RingDisplay;

    public DescriptionUI.Data GetDescription() => RingDisplay.GetDescription();
}