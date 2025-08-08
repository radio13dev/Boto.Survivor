using System.Text;
using TMPro;
using UnityEngine;

public class Palette : MonoBehaviour
{
    public static Color GemSize(int size)
    {
        return size switch
        {
            1 => new Color(0.2f, 0.8f, 0.2f),
            2 => new Color(0.8f, 0.8f, 0.2f),
            3 => new Color(0.8f, 0.4f, 0.2f),
            _ => Color.white
        };
    }
}

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
            }
            else
            {
                // Inventory
                sb.AppendLine("(Inventory)".Color(Color.gray).Size(30));
            }
            
            if (gem.Gem.IsValid)
            {
            
                if (UIFocus.Interact && UIFocus.Interact.TryGetComponent<GemDisplay>(out var heldGem))
                {
                    sb.AppendLine("COMBINE".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
                    sb.AppendLine("Multishot Gem".Size(36));
                    sb.AppendLine(("Size: " + "1".Color(Palette.GemSize(1)) + "→" + "2".Color(Palette.GemSize(2))).Size(30));
                }
                else
                {
                    sb.AppendLine("Multishot Gem".Size(36));
                    sb.AppendLine("Size: 1".Size(30));
                }
            }
            else
            {
                sb.AppendLine("Empty Slot".Color(new Color(0.2f,0.2f,0.2f)).Size(30));
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
