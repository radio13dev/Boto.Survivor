using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[ExecuteAlways]
public class WorldSpaceCanvasScaler : MonoBehaviour
{
    float m_WidthOld;
    float m_HeightOld;
    Canvas m_Canvas;
    Canvas Canvas => m_Canvas ? m_Canvas : m_Canvas = GetComponent<Canvas>();

    private void OnValidate()
    {
        Setup();
    }

    private void OnEnable()
    {
        Setup();
    }

    private void Update()
    {
        if (m_WidthOld != Screen.width || m_HeightOld != Screen.height)
        {
            Setup();
        }
    }

    [EditorButton]
    void Setup()
    {
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        m_WidthOld = Screen.width;
        m_HeightOld = Screen.height;

        // Multiple display support only when not the main display. For display 0 the reported
        // resolution is always the desktops resolution since its part of the display API,
        // so we use the standard none multiple display method. (case 741751)
        int displayIndex = Canvas.targetDisplay;
        if (displayIndex > 0 && displayIndex < Display.displays.Length)
        {
            Display disp = Display.displays[displayIndex];
            screenSize = new Vector2(disp.renderingWidth, disp.renderingHeight);
        }


        float scaleFactor = 0;
        scaleFactor = Mathf.Min(screenSize.x / 1920, screenSize.y / 1080);


        transform.localScale = scaleFactor * Vector3.one;
        var rect = (RectTransform)transform;
        rect.sizeDelta = screenSize / scaleFactor;
        rect.position = new Vector3(739, 739 * screenSize.y / screenSize.x, 0);
    }
}