using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TiledStatsUITile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IFocusFilter, DescriptionUI.ISource
{
    public Image Background;
    public Image Border;
    public Image Image;
    public TMP_Text LevelText;

    public Image NotAvailableOverlay;
    public Image NotEnoughMoneyOverlay;

    public Image HasPointOutline;
    public Image MaxedOutline;

    public Image[] Connections = new Image[4];

    public PooledParticle LevelUpParticle;

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Send drag movement to parent ui
        //var ui = GetComponentInParent<TiledStatsUI>();
        //if (ui)
        //{
        //    ui.ApplyDragDelta(eventData.delta);
        //}
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (UIFocus.Focus == gameObject)
        {
            var ui = GetComponentInParent<TiledStatsUI>();
            if (ui)
            {
                ui.MoveTowards(Offset);

                if (DescriptionUI.m_CustomZero != gameObject)
                    DescriptionUI.m_CustomZero = gameObject;
                else if (!NotAvailableOverlay.gameObject.activeSelf)
                {
                    // Level up!
                    var old = Level;
                    if (Keyboard.current.leftShiftKey.isPressed)
                        Level = Level + 1;
                    else
                        Level = math.max(Level, math.min(5, Level + 1));

                    if (old != Level)
                    {
                        ui.m_unlocked[TileKey] = Level;
                        ui.RebuildTiles();

                        var particle = LevelUpParticle.GetFromPool();
                        particle.transform.SetPositionAndRotation(transform.position, transform.rotation);
                        particle.transform.localScale = Vector3.one * 100;
                        
                        UIFocus.Refresh();
                    }
                }
            }
        }
    }

    public Vector2 Offset;

    public void SetOffset(Vector2 offset)
    {
        Offset = offset;
    }

    public void SetImages(Sprite body, Sprite bodyOutline, Sprite icon)
    {
        NotAvailableOverlay.sprite = NotEnoughMoneyOverlay.sprite = Background.sprite = body;
        HasPointOutline.sprite = MaxedOutline.sprite = Border.sprite = bodyOutline;
        Image.sprite = icon;
    }

    Vector2Int TileKey;
    int Level;

    public void RefreshState(Vector2Int tileKey, ref Dictionary<Vector2Int, int> mUnlocked)
    {
        TileKey = tileKey;
        Level = mUnlocked.GetValueOrDefault(tileKey);

        LevelText.text = $"{Level}/{5}";

        MaxedOutline.gameObject.SetActive(Level >= 5);
        HasPointOutline.gameObject.SetActive(Level > 0 && Level < 5);
        NotEnoughMoneyOverlay.gameObject.SetActive(false);

        bool locked = Level == 0;
        if (!locked)
        {
            Connections[0].gameObject.SetActive(true);
            Connections[1].gameObject.SetActive(true);
            Connections[2].gameObject.SetActive(true);
            Connections[3].gameObject.SetActive(true);
        }
        else
        {
            if (mUnlocked.ContainsKey(mathu.modabs(tileKey + Vector2Int.up, TiledStats.TileCount)))
            {
                locked = false;
                Connections[0].gameObject.SetActive(true);
            }
            else
            {
                Connections[0].gameObject.SetActive(false);
            }

            if (mUnlocked.ContainsKey(mathu.modabs(tileKey + Vector2Int.right, TiledStats.TileCount)))
            {
                locked = false;
                Connections[1].gameObject.SetActive(true);
            }
            else
            {
                Connections[1].gameObject.SetActive(false);
            }

            if (mUnlocked.ContainsKey(mathu.modabs(tileKey + Vector2Int.down, TiledStats.TileCount)))
            {
                locked = false;
                Connections[2].gameObject.SetActive(true);
            }
            else
            {
                Connections[2].gameObject.SetActive(false);
            }

            if (mUnlocked.ContainsKey(mathu.modabs(tileKey + Vector2Int.left, TiledStats.TileCount)))
            {
                locked = false;
                Connections[3].gameObject.SetActive(true);
            }
            else
            {
                Connections[3].gameObject.SetActive(false);
            }
        }

        NotAvailableOverlay.gameObject.SetActive(locked);
    }

    public bool CheckFocusFilter(GameObject go)
    {
        return go == null;
    }

    public void GetDescription(out string title, out string description, out List<(string left, string oldVal, float change, string newVal)> rows,
        out (string left, string right) bottomRow)
    {
        if (DescriptionUI.m_CustomZero != gameObject)
        {
            DescriptionUI.m_CustomZero.GetComponent<TiledStatsUITile>().GetDescription(out title, out description, out rows, out bottomRow);
            return;
        }

        var stat = TiledStats.Get(TileKey);
        title = stat.GetTitle();
        description = stat.GetDescription();

        if (Level == 0 || Level >= 5)
            rows = stat.GetDescriptionRows(math.max(Level, 1));
        else
            rows = stat.GetDescriptionRows(Level, Level + 1);

        if (Level >= 5)
        {
            // Maxed out
            bottomRow = ($"Maxed!", "-");
        }
        else
        {
            // Cost
            bottomRow = ($"Cost", "2000\u00a9".Color(Palette.Money));
        }
    }
}