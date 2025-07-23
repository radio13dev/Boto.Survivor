using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HandUI : Selectable, ICancelHandler
{
    public static HandUI Instance;
    public static bool IsOpen => Instance && Instance.m_IsOpen;
    
    public FingerUI[] Fingers;
    public MapUI Map;
    
    bool m_IsOpen;

    protected override void Start()
    {
        base.Start();
        
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        foreach (var finger in Fingers)
        {
            finger.OnSelect(default);
            finger.OnDeselect(default);
        }
        Map.OnDeselect(default);
        Map.OnDeselect(default);
        
        Close();
    }

    public static void Close()
    {
        Home();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        Instance.StartCoroutine(Instance.HideCo());
    }
    
    public static void Open()
    {
        Instance.StartCoroutine(Instance.ShowCo());
        Home();
    }

    public static void Home()
    {
        Instance.Select();
    }

    public void OnCancel(BaseEventData eventData)
    {
        Close();
    }

    private void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (IsOpen) Close();
            else Open();
        }
    }
    
    const float k_ClosedOffset = -500;
    const float k_OpenOffset = 0;
    const float k_OpenDuration = 0.2f;
    IEnumerator ShowCo()
    {
        if (m_IsOpen) yield break;
        
        m_IsOpen = true;
        var rect = (RectTransform)transform;
        float t = 0;
        while (t < k_OpenDuration)
        {
            if (!m_IsOpen) yield break;
            
            t += Time.deltaTime;
            t = Mathf.Clamp01(t);
            rect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, math.lerp(k_ClosedOffset, k_OpenOffset, t/k_OpenDuration), rect.rect.height);
            yield return null;
        }
    }
    IEnumerator HideCo()
    {
        if (!m_IsOpen) yield break;
        
        m_IsOpen = false;
        var rect = (RectTransform)transform;
        float t = 0;
        while (t < k_OpenDuration)
        {
            if (m_IsOpen) yield break;
            
            t += Time.deltaTime;
            t = Mathf.Clamp01(t);
            rect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, math.lerp(k_ClosedOffset, k_OpenOffset, 1-t/k_OpenDuration), rect.rect.height);
            yield return null;
        }
    }
}