using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LootPopupOption : Selectable, IPointerClickHandler, ISubmitHandler
{
    public int OptionIndex;
    public GameObject InteractionNotification;
    public GameObject HeldHighlight;
    public TMP_Text Description;

    public void Setup(RingStats ringStats)
    {
        Description.text = ringStats.PrimaryEffect.ToString();
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        OnSubmit(eventData);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (HandUIController.LastPressed != this)
        {
            HandUIController.LastPressed = this;
            for (int i = 0; i < FingerUI.Instances.Length; i++)
            {
                if (!FingerUI.Instances[i].Ring.isActiveAndEnabled)
                {
                    FingerUI.Instances[i].Select();
                    return;
                }
            }
            FingerUI.Instances[0].Select();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (HandUIController.LastPressed == this) HandUIController.LastPressed = null;
    }
}