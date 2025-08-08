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
        GameObject interact = UIFocus.Interact;
        GameObject focus = null;
        if (UIFocus.Interact && (!UIFocus.Focus || !UIFocus.CheckFocus(UIFocus.Focus)))
            focus = interact;
        else
            focus = UIFocus.Focus;
        
        if (!focus)
        {
            Visual.gameObject.SetActive(false);
            return;
        }
        else
        {
            Visual.gameObject.SetActive(true);
        }
        
        StringBuilder sb = new();
        if (focus.TryGetComponent<GemDisplay>(out var gem))
        {
            if (gem.IsInSlot)
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
                if (interact && interact != focus && interact.TryGetComponent<GemDisplay>(out var heldGem))
                {
                    if (gem.Gem.ClientId != heldGem.Gem.ClientId)
                    {
                        sb.AppendLine("SWAP".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
                    }
                    else if (focus == null)
                    {
                        sb.AppendLine("UNSOCKET".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
                    }
                }
                
                sb.AppendLine(gem.Gem.GetTitleString().Size(36));
                sb.AppendLine($"Size: {gem.Gem.Size.Color(Palette.GemSize(gem.Gem.Size))}".Size(30));
            }
            else if (interact && interact.TryGetComponent<GemDisplay>(out var heldGem))
            {
                sb.AppendLine("INSERT".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
                sb.AppendLine(heldGem.Gem.GetTitleString().Size(36));
                sb.AppendLine($"Size: {heldGem.Gem.Size.Color(Palette.GemSize(gem.Gem.Size))}".Size(30));
            }
            else
            {
                sb.AppendLine("Empty Slot".Color(new Color(0.2f,0.2f,0.2f)).Size(30));
            }
        }
        else if (focus.TryGetComponent<RingDisplay>(out var ring))
        {
            // Inventory
            sb.AppendLine("(Rings)".Color(Color.gray).Size(30));
            
            if (interact && interact != focus && interact.TryGetComponent<RingDisplay>(out var heldRing))
            {
                sb.AppendLine("SWAP".Size(36).Color(new Color(0.9960785f, 0.4313726f, 0.3254902f)));
            }
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
