using UnityEngine;

public class LootRewardUI_Item : MonoBehaviour, DescriptionUI.ISource
{
    public RingDisplay RingDisplay;

    public DescriptionUI.Data GetDescription()
    {
        var data =  RingDisplay.GetDescription();
        
        data.ButtonText = "Accept";
        data.ButtonPress = () => GetComponentInParent<LootRewardUI>().Accept();
        data.ButtonPress1 = null;
        
        return data;
    }
}