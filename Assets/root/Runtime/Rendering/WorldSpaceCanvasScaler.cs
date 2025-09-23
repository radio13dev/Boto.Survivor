using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[ExecuteAlways]
public class WorldSpaceCanvasScaler : CanvasScaler
{
    Canvas m_Canvas;
    Canvas Canvas => m_Canvas ? m_Canvas : m_Canvas = GetComponent<Canvas>();
    protected override void OnEnable()
    {
        base.OnEnable();
        m_Canvas = GetComponent<Canvas>();
    }

    protected override void HandleWorldCanvas()
    {
        if (!Application.isPlaying)
        {
            base.HandleWorldCanvas();
            return;
        }
        
        switch (uiScaleMode)
        {
            case ScaleMode.ConstantPixelSize: HandleConstantPixelSize(); break;
            case ScaleMode.ScaleWithScreenSize: HandleScaleWithScreenSize(); break;
            case ScaleMode.ConstantPhysicalSize: HandleConstantPhysicalSize(); break;
        }
    }

    protected override void HandleScaleWithScreenSize()
    {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);

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
            switch (m_ScreenMatchMode)
            {
                case ScreenMatchMode.MatchWidthOrHeight:
                {
                    // We take the log of the relative width and height before taking the average.
                    // Then we transform it back in the original space.
                    // the reason to transform in and out of logarithmic space is to have better behavior.
                    // If one axis has twice resolution and the other has half, it should even out if widthOrHeight value is at 0.5.
                    // In normal space the average would be (0.5 + 2) / 2 = 1.25
                    // In logarithmic space the average is (-1 + 1) / 2 = 0
                    float logWidth = Mathf.Log(screenSize.x / m_ReferenceResolution.x, 2);
                    float logHeight = Mathf.Log(screenSize.y / m_ReferenceResolution.y, 2);
                    float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, m_MatchWidthOrHeight);
                    scaleFactor = Mathf.Pow(2, logWeightedAverage);
                    break;
                }
                case ScreenMatchMode.Expand:
                {
                    scaleFactor = Mathf.Min(screenSize.x / m_ReferenceResolution.x, screenSize.y / m_ReferenceResolution.y);
                    break;
                }
                case ScreenMatchMode.Shrink:
                {
                    scaleFactor = Mathf.Max(screenSize.x / m_ReferenceResolution.x, screenSize.y / m_ReferenceResolution.y);
                    break;
                }
            }

            SetScaleFactor(scaleFactor);
            transform.localScale = scaleFactor * Vector3.one;
            
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
            var rect = (RectTransform)transform;
            rect.sizeDelta = screenSize/scaleFactor;
            rect.position = new Vector3(739, 739*screenSize.y/screenSize.x, 0);
            
    }
}
