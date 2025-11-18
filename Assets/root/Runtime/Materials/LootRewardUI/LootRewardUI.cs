using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LootRewardUI : MonoBehaviour, IPointerClickHandler, HandUIController.IStateChangeListener
{
    static event Action s_DoOnceOnClose;
    
    public LootRewardUI_Item[] RewardTemplates = Array.Empty<LootRewardUI_Item>();
    public Transform RewardContainer;
    public LootRewardUI_Item[] ActiveItems => RewardContainer.GetComponentsInChildren<LootRewardUI_Item>();
    
    public int TargetIndex;
    public float FocusedIndex;
    public float2 AnimFocusedIndex => FocusedIndex + m_RevealAnimOffset;
    
    [Header("Torus Mesh Stuff")]
    public TorusTerrainTool TerrainTool;
    public float Radius = 8;
    public float Thickness = 3.5f;
    public float ItemOffset = 0.2f;
    public int MeshRingSegments = 10;
    public int MeshTubeSegments = 10;
    
    [Header("Reveal Animation")]
    ExclusiveCoroutine m_RevealItemsCo;
    float2 m_ZeroOffset;
    public float2 InitZero;
    public float2 ZeroOffset = new float2(math.PI, math.PIHALF);
    
    float2 m_ItemGrouping;
    public float2 InitItemGrouping = 1f;
    public float2 ItemGrouping = new float2(0.7f, 0.4f);
    
    float2 m_ItemGroupingSmoothing;
    public float2 InitItemGroupingSmoothing = 1f;
    public float2 ItemGroupingSmoothing = new float2(1f, 1f);
    
    float2 m_RevealAnimOffset;
    public float2 InitRevealAnimOffset = new float2(0, 2f);
    
    public float InitTime = 0.2f;
    public float Duration = 1f;
    public float FocusDelay = 0.8f;
    public ease.Mode EasingMode;
    
    [Header("Navigation Animation")]
    ExclusiveCoroutine m_Co;
    public float RelativeSpeed = 1f;
    public float LinearSpeed = 1f;

    // Close on first awake
    private void Awake()
    {
        HandUIController.Attach(this);
        Clear();
        AddRewards();
        AddRewards();
        SetIndex(default);
        RevealItems();
        Close();
    }

    private void OnDestroy()
    {
        HandUIController.Detach(this);
    }

    private void OnEnable()
    {
    }

    private void OnDisable()
    {
        ClearFocus();
    }

    public static void OpenUI(Action DoOnceOnClose)
    {
        s_DoOnceOnClose += DoOnceOnClose;
        HandUIController.SetState(HandUIController.State.Skills);
    }

    [EditorButton]
    public void Open()
    {
        HandUIController.SetState(HandUIController.State.Skills);
    }
    
    [EditorButton]
    public void Close()
    {
        HandUIController.SetState(HandUIController.State.Closed);
    }
    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        if (newState == HandUIController.State.Skills)
        {
            if (oldState != HandUIController.State.Skills)
            {
                ClearFocus();
                RevealItems();
            }
        }
        else
        {
            if (Application.isPlaying)
            {
                ClearFocus();
                var close = s_DoOnceOnClose;
                s_DoOnceOnClose = null;
                close?.Invoke();
            }
        }
    }

    public const float KeyboardInputCD = 0.1f;
    public float TimeSinceKeyboardInput = 0f;
    [EditorButton]
    private void Update()
    {
        if (HandUIController.GetState() == HandUIController.State.Loot)
        {
            var dir = GameInput.Inputs.UI.Navigate.ReadValue<Vector2>();
            if (math.abs(dir.x) >= 0.3f)
            {
                if (TimeSinceKeyboardInput > 0)
                {
                    TimeSinceKeyboardInput -= Time.deltaTime;
                }
                else
                {
                    // Get the current focused tile and select the one in the direction of input
                    SetIndex((dir.x > 0 ? 1 : -1) + TargetIndex);
                    TimeSinceKeyboardInput = KeyboardInputCD;
                }
            }
            else
            {
                TimeSinceKeyboardInput = 0;
            }
        }
    
        // Update positions of spawned
        var items = ActiveItems;
        var torus = new Torus(Radius, 0.01f);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var pos = GetToroidalForXY(index, 0, items.Length);
            item.transform.localPosition = torus.ToroidalToCartesian(pos);
        }
    }
    
    [EditorButton]
    public void RebuildMesh()
    {
        TerrainTool.GenerateMesh(Radius, Thickness, MeshRingSegments, MeshTubeSegments);
        Clear();
        AddRewards();
    }
    
    [EditorButton]
    public void Clear()
    {
        var toClear = ActiveItems;
        foreach (var item in toClear)
        {
            if (Application.isPlaying) Destroy(item.gameObject); else DestroyImmediate(item.gameObject);
        }
    }
    
    [EditorButton]
    public void AddRewards()
    {
        if (RewardTemplates.Length == 0) return;
        
        Unity.Mathematics.Random r = Unity.Mathematics.Random.CreateFromIndex((uint)(Random.value*1000));
        int toSpawn = Random.Range(3,6);
        for (int i = 0; i < toSpawn; i++)
        {
            var spawned = Instantiate(RewardTemplates[Random.Range(0,RewardTemplates.Length)], RewardContainer);
            spawned.gameObject.SetActive(true);
            spawned.RingDisplay.UpdateRing(-1, new Ring(){ Stats = RingStats.Generate(ref r) }, true);
        }
    }

    [EditorButton]
    public void RevealItems()
    {
        m_RevealItemsCo.StartCoroutine(this, RevealItemsCo());
    }
    IEnumerator RevealItemsCo()
    {
#if UNITY_EDITOR
        var t0 = EditorApplication.timeSinceStartup - InitTime;
#endif
        float t = InitTime;
        while (t < Duration)
        {
            bool focus = t < FocusDelay;
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
                t = (float)(EditorApplication.timeSinceStartup - t0);
            else
#endif
            t += Time.deltaTime;
            t = math.clamp(t, 0, Duration);
            if (focus && t >= FocusDelay)
                StartFocus();
                
            var tEase = EasingMode.Evaluate(t/Duration);
            m_ZeroOffset = mathu.lerprepeat(InitZero, ZeroOffset, tEase, math.PI);
            m_ItemGrouping = math.lerp(InitItemGrouping, ItemGrouping, tEase);
            m_ItemGroupingSmoothing = math.lerp(InitItemGroupingSmoothing, ItemGroupingSmoothing, tEase);
            m_RevealAnimOffset = math.lerp(InitRevealAnimOffset, 0, tEase);
            yield return null;
        }
        StartFocus(); // Fallback
    }
    
    [EditorButton]
    public void SetIndex(int index)
    {
        // Unfocus current
        ClearFocus();
        TargetIndex = (int)mathu.repeat(index, ActiveItems.Length);
        SetPosition(TargetIndex);
        StartFocus();
    }

    public void SetPosition(float position)
    {
        m_Co.StartCoroutine(this, _SetPositionCo(position));
    }

    IEnumerator _SetPositionCo(float position)
    {
        float t = 0;
        var length = ActiveItems.Length/2f;
        while (t < 10f)
        {
            var old = FocusedIndex;
            FocusedIndex = mathu.lerprepeat(FocusedIndex, position, Time.deltaTime*RelativeSpeed, length);
            FocusedIndex += mathu.deltarepeat(FocusedIndex, position, length) * math.clamp(Time.deltaTime * LinearSpeed, 0, 1f);
            if (old == FocusedIndex) break;
            t += Time.deltaTime;
            yield return null;
        }

        FocusedIndex = mathu.repeat(position, length * 2);
    }
    
    private void ClearFocus()
    {
        var index = TargetIndex;
        var tiles = ActiveItems;
        if (index >= 0 && index < tiles.Length && tiles[index].TryGetComponent<Focusable>(out var focusable))
        {
            if (Application.isPlaying) UIFocus.EndFocus(focusable);
        }
    }
    private void StartFocus()
    {
        var index = TargetIndex;
        var tiles = ActiveItems;
        if (index >= 0 && index < tiles.Length && tiles[index].TryGetComponent<Focusable>(out var focusable))
        {
            if (Application.isPlaying) UIFocus.StartFocus(focusable);
        }
    }
    private float2 GetToroidalForXY(float x, float y, int rewardCount)
    {
        float2 toroidal = new float2((x - AnimFocusedIndex.x) * math.PI2 / rewardCount, (y - AnimFocusedIndex.y) * math.PI2);
        
        if (m_ItemGrouping.x >= 0)
        {
            toroidal.x = mathu.lerprepeat(toroidal.x, 0,
                m_ItemGrouping.x * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.x * math.abs(mathu.deltaangle(math.PI, toroidal.x)) / math.PI), 0, 1), math.PI);
        }
        else
        {
            toroidal.x = mathu.lerprepeat(toroidal.x, math.PI,
                -m_ItemGrouping.x * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.x * math.abs(mathu.deltaangle(0, toroidal.x)) / math.PI), 0, 1), math.PI);
        }
        toroidal.y = mathu.lerprepeat(toroidal.y, 0,
            m_ItemGrouping.y * math.clamp(ease.cubic_out(m_ItemGroupingSmoothing.y * math.abs(mathu.deltaangle(math.PI, toroidal.y)) / math.PI), 0, 1), math.PI);
        
        
        toroidal.x += m_ZeroOffset.x;
        toroidal.y -= m_ZeroOffset.y;
        return toroidal;
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject.TryGetComponent<LootRewardUI_Item>(out var item))
        {
            // Try find the index of this tile
            var tiles = ActiveItems;
            var index = Array.IndexOf(tiles, item);
            if (index >= 0)
            {
                if (index == TargetIndex)
                {
                    //UnlockTile(tile);
                }
                else
                    SetIndex(index);
            }
        }
    }
}
