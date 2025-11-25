using UnityEngine;
using UnityEngine.UI;

public class StatIcon : MonoBehaviour
{
    public Image Icon;
    public Image Background;
    public Image Border;
    
    public void SetStat(TiledStat stat)
    {
        var def = stat.GetFull();
        SetImages(def.Images.Shape, def.Images.ShapeOutline, def.Images.Icon);
        SetColor(def.Images.Color);
    }

    public void SetImages(Sprite body, Sprite bodyOutline, Sprite icon)
    {
        Background.sprite = body;
        Border.sprite = bodyOutline;
        Icon.sprite = icon;
    }

    public void SetColor(Color rowColor)
    {
        Background.color = rowColor;
    }
}