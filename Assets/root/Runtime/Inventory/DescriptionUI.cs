using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Entities;
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
    
    public static Color Money => new Color(0.2f, 0.8f, 0.2f);
    public static Color MoneyChangePositive = new Color(0.2f, 0.8f, 0.2f);
    public static Color MoneyChangeNegative = new Color32(0xff,0x72,0x77,0xff);
    
    public static Color HealthChangeZero => new Color(0.7f, 0.7f, 0.7f);
    public static Color HealthChangePositive = new Color(0.4f, 0.7f, 0.4f);
    public static Color HealthChangeNegative = new Color(0.7f, 0.4f, 0.4f);
}

public class DescriptionUI : MonoBehaviour
{
    public interface ISource
    {
        void GetDescription(out string title, 
            out string description, 
            out List<(string left, string oldVal, float change, string newVal)> rows, 
            out (string left, eBottomRowIcon icon, string right) bottomRow);
    }
    
    public enum eBottomRowIcon
    {
        None,
        Gem
    }

    public TMP_Text Title;
    public TMP_Text Description;
    public List<DescriptionUIRow> Rows;
    public TMP_Text BottomRowLeft;
    public GameObject[] BottomRowIcons;
    public TMP_Text BottomRowRight;
    
    public static GameObject m_CustomZero; 

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
            focus = m_CustomZero;
        
        if (!focus)
        {
            SetText("Info...", "Description...");
            return;
        }
        else if (!focus.TryGetComponent<ISource>(out var desc))
        {
            SetText($"{focus.gameObject.name}", $"A {focus} UI Element!");
        }
        else
        {
            SetText(desc);
        }
    }

    private void SetText(ISource component)
    {
        component.GetDescription(out string info, 
            out string description, 
            out List<(string left, string oldVal, float change, string newVal)> rows, 
            out (string left, eBottomRowIcon icon, string right) bottomRow);
        
        SetText(info, description, rows, bottomRow);
    }

    private void SetText(
        string title, 
        string description, 
        List<(string left, string oldVal, float change, string newVal)> rows = default, 
        (string left, eBottomRowIcon icon, string right) bottomRow = default)
    {
        Title.text = title;
        
        if (!string.IsNullOrEmpty(description))
        {
            Description.text = description;
            Description.gameObject.SetActive(true);
        }
        else
            Description.gameObject.SetActive(false);
        
        if (rows?.Count > 0)
        {
            int i;
            for (i = 0; i < rows.Count; i++)
            {
                if (i >= Rows.Count)
                    Rows.Add(Instantiate(Rows[0], Rows[0].transform.parent));
                Rows[i].Left.text = rows[i].left;
                if (string.IsNullOrEmpty(rows[i].oldVal) || rows[i].oldVal == rows[i].newVal)
                {
                    Rows[i].OldVal.gameObject.SetActive(false);
                    Rows[i].NegativeChange.gameObject.SetActive(false);
                    Rows[i].PositiveChange.gameObject.SetActive(false);
                    Rows[i].NewVal.text = rows[i].newVal;
                }
                else
                {
                    Rows[i].OldVal.gameObject.SetActive(true);
                    Rows[i].OldVal.text = rows[i].oldVal;
                    Rows[i].NewVal.text = rows[i].newVal;
                    if (rows[i].change >= 0)
                    {
                        Rows[i].PositiveChange.gameObject.SetActive(true);
                        Rows[i].NegativeChange.gameObject.SetActive(false);
                        //Rows[i].PositiveChange.fillAmount = Mathf.Clamp01(rows[i].change / 10f);
                    }
                    else if (rows[i].change < 0)
                    {
                        Rows[i].PositiveChange.gameObject.SetActive(false);
                        Rows[i].NegativeChange.gameObject.SetActive(true);
                        //Rows[i].NegativeChange.fillAmount = Mathf.Clamp01(-rows[i].change / 10f);
                    }
                }
                
                Rows[i].gameObject.SetActive(true);
            }
            for (; i < Rows.Count; i++)
            {
                Rows[i].gameObject.SetActive(false);
            }
            
            Rows[0].transform.parent.gameObject.SetActive(true);
        }
        else
        {
            Rows[0].transform.parent.gameObject.SetActive(false);
        }
        
        if (bottomRow != default)
        {
            BottomRowLeft.text = bottomRow.left;
            for (int i = 0; i < BottomRowIcons.Length; i++)
            {
                if ((int)bottomRow.icon == i)
                    BottomRowIcons[i].SetActive(true);
                else
                    BottomRowIcons[i].SetActive(false);
            }
            BottomRowRight.text = bottomRow.right;
            BottomRowLeft.transform.parent.gameObject.SetActive(true);
        }
        else
        {
            BottomRowLeft.transform.parent.gameObject.SetActive(false);
        }
    }
}
