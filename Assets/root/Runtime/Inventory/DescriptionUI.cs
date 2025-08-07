using System.Text;
using TMPro;
using UnityEngine;

public class DescriptionUI : MonoBehaviour
{
    public Transform Visual;
    public TMP_Text Description;

    private void OnEnable()
    {
        UIFocus.OnFocus += OnFocus;
    }

    private void OnDisable()
    {
        UIFocus.OnFocus -= OnFocus;
    }

    private void OnFocus()
    {
        if (UIFocus.Interact && !UIFocus.Focus) return;
    
        if (!UIFocus.Focus)
        {
            Visual.gameObject.SetActive(false);
            return;
        }
        else
        {
            Visual.gameObject.SetActive(true);
        }
        
        StringBuilder sb = new();
        if (UIFocus.Focus.TryGetComponent<GemDisplay>(out var gem))
        {
            if (gem.GetComponentInParent<RingFocusDisplay>())
            {
                // Equipped
                sb.AppendLine("(Equipped)".Color(Color.gray).Size(30));
                if (gem.Gem.IsValid)
                {
                    sb.AppendLine("Multishot Gem".Size(36));
                    sb.AppendLine("Size: 1".Size(30));
                }
                else
                {
                    sb.AppendLine("Empty Slot".Color(new Color(0.2f,0.2f,0.2f)).Size(30));
                }
            }
            else
            {
                // Inventory
                sb.AppendLine("(Inventory)".Color(Color.gray).Size(30));
                sb.AppendLine("Multishot Gem".Size(36));
                sb.AppendLine("Size: 1".Size(30));
            }
        }
        else if (UIFocus.Focus.TryGetComponent<RingDisplay>(out var ring))
        {
            // Inventory
            sb.AppendLine("(Rings)".Color(Color.gray).Size(30));
            if (ring.Ring.Stats.IsValid)
            {
                sb.AppendLine($"{ring.Ring.Stats.GetTitleString()}".Size(36));
                sb.AppendLine($"{ring.Ring.Stats.GetDescriptionString()}".Size(30));
            }
            else
            {
                sb.AppendLine("Empty Slot".Color(new Color(0.2f,0.2f,0.2f)).Size(30));
            }
        }
        
        Description.text = sb.ToString();
    }
}
