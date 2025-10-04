using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AYellowpaper.SerializedCollections;
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
        Data GetDescription();
    }
    
    public enum eBottomRowVariant
    {
        None,
        BuyInvalid,
        BuyValid,
        SwapRing,
    }

    public struct Data
    {
        public string Title;
        public string Description;
        public List<Row> Rows;
        
        public string BottomLeft;
        public string BottomRight;
        public eBottomRowVariant BottomVariant;
        
        public TiledStatData[] TiledStatsData;
        
        public struct TiledStatData
        {
            public TiledStat Stat;
            public int Low;
            public int Boost;
            public RingDisplay RingDisplayParent;
            public bool IsValid => Boost != 0;
        }
        
        public Action ButtonPress;
        
        public struct Row
        {
            public string Left;
            public string OldVal;
            public float Change;
            public string NewVal;

            public Row(string left, string oldVal, float change, string newVal)
            {
                Left = left;
                OldVal = oldVal;
                Change = change;
                NewVal = newVal;
            }
        }

        public static Data Demo(string title, string description)
        {
            return new Data()
            {
                Title = title,
                Description = description
            };
        }
    }

    public TMP_Text Title;
    public TMP_Text Description;
    public List<DescriptionUIRow> Rows;
    public TMP_Text BottomRowLeft;
    public TMP_Text BottomRowRight;
    
    public SerializedDictionary<eBottomRowVariant, GameObject[]> BottomRowVariants = new();
    
    public ChoiceUIRow[] ChoiceUIElements = Array.Empty<ChoiceUIRow>();
    
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
            SetText(Data.Demo("Info...", "Description..."));
            return;
        }
        else if (!focus.TryGetComponent<ISource>(out var desc))
        {
            SetText(Data.Demo($"{focus.gameObject.name}", $"A {focus} UI Element!"));
        }
        else
        {
            SetText(desc);
        }
    }

    public void SetText(ISource component)
    {
        var data = component.GetDescription();
        
        SetText(data);
    }
    
    Action m_ButtonPress;
    public void ButtonPress()
    {
        m_ButtonPress?.Invoke();
    }

    public void SetText(Data data)
    {
        m_ButtonPress = data.ButtonPress;
        
        Title.text = data.Title;
        
        if (!string.IsNullOrEmpty(data.Description))
        {
            Description.text = data.Description;
            Description.gameObject.SetActive(true);
        }
        else
            Description.gameObject.SetActive(false);
        
        if (data.Rows?.Count > 0)
        {
            int i;
            for (i = 0; i < data.Rows.Count; i++)
            {
                if (i >= Rows.Count)
                    Rows.Add(Instantiate(Rows[0], Rows[0].transform.parent));
                Rows[i].Left.text = data.Rows[i].Left;
                if (string.IsNullOrEmpty(data.Rows[i].OldVal) || data.Rows[i].OldVal == data.Rows[i].NewVal)
                {
                    Rows[i].OldVal.gameObject.SetActive(false);
                    Rows[i].NegativeChange.gameObject.SetActive(false);
                    Rows[i].PositiveChange.gameObject.SetActive(false);
                    Rows[i].NewVal.text = data.Rows[i].NewVal;
                }
                else
                {
                    Rows[i].OldVal.gameObject.SetActive(true);
                    Rows[i].OldVal.text = data.Rows[i].OldVal;
                    Rows[i].NewVal.text = data.Rows[i].NewVal;
                    if (data.Rows[i].Change >= 0)
                    {
                        Rows[i].PositiveChange.gameObject.SetActive(true);
                        Rows[i].NegativeChange.gameObject.SetActive(false);
                        //Rows[i].PositiveChange.fillAmount = Mathf.Clamp01(rows[i].change / 10f);
                    }
                    else if (data.Rows[i].Change < 0)
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
        
        if (!string.IsNullOrEmpty(data.BottomLeft))
        {
            BottomRowLeft.text = data.BottomLeft;
            BottomRowRight.text = data.BottomRight;
            BottomRowRight.gameObject.SetActive(!string.IsNullOrEmpty(data.BottomRight));
            
            foreach (var v in BottomRowVariants)
            foreach (var o in v.Value)
                o.SetActive(false);
            foreach (var o in BottomRowVariants[data.BottomVariant])
                o.SetActive(true);
                
            BottomRowLeft.transform.parent.gameObject.SetActive(true);
        }
        else
        {
            BottomRowLeft.transform.parent.gameObject.SetActive(false);
        }
        
        for (int i = 0; i < ChoiceUIElements.Length; i++)
        {
            if (i < data.TiledStatsData?.Length && data.TiledStatsData[i].IsValid)
            {
                ChoiceUIElements[i].gameObject.SetActive(true);
                ChoiceUIElements[i].Setup(data.TiledStatsData[i].Stat, data.TiledStatsData[i].Low, data.TiledStatsData[i].Boost, data.TiledStatsData[i].RingDisplayParent);
            }
            else
                ChoiceUIElements[i].gameObject.SetActive(false);
        }
    }
}
