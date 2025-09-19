using System.Collections.Generic;
using BovineLabs.Core.Extensions;
using BovineLabs.Saving;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour, HandUIController.IStateChangeListener
{
    public TransitionPoint ClosedT;
    public TransitionPoint InventoryT;
    ExclusiveCoroutine Co;

    Dictionary<uint, GemDisplay> m_InventoryGems = new();
    public GemDisplay GemDisplayPrefab;
    public RingDisplay[] RingDisplays;
    public RingFocusDisplay RingFocusDisplay;

    private void Awake()
    {
        GemDisplayPrefab.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        HandUIController.Attach(this);

        UIFocus.OnFocus += OnFocus;
        UIFocus.OnInteract += OnInteract;
        GameEvents.OnEvent += OnGameEvent;
        if (CameraTarget.MainTarget) OnGameEvent(new (GameEvents.Type.InventoryChanged, CameraTarget.MainTarget.Entity));
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);

        UIFocus.OnFocus -= OnFocus;
        UIFocus.OnInteract -= OnInteract;
        GameEvents.OnEvent -= OnGameEvent;
    }

    private void OnFocus()
    {
        //if (UIFocus.Focus && UIFocus.Focus.TryGetComponent<RingDisplay>(out var display))
        //{
        //    var index = Array.IndexOf(RingDisplays, display);
        //    if (index != -1)
        //    {
        //        RingFocusDisplay.UpdateRing(index, display);
        //    }
        //}
    }

    public float _InnerCursorRadius;
    public float _InnerLeniencyRadius;
    public float _OuterLeniencyRadius;
    public float _OuterCursorRadius;
    
    public float InnerCursorRadius => _InnerCursorRadius * transform.lossyScale.x;
    public float InnerLeniencyRadius => _InnerLeniencyRadius * transform.lossyScale.x;
    public float OuterLeniencyRadius => _OuterLeniencyRadius * transform.lossyScale.x;
    public float OuterCursorRadius => _OuterCursorRadius * transform.lossyScale.x;

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(RingFocusDisplay.transform.position, InnerCursorRadius);
        Gizmos.DrawWireSphere(RingFocusDisplay.transform.position, InnerLeniencyRadius);
        Gizmos.DrawWireSphere(RingFocusDisplay.transform.position, OuterLeniencyRadius);
        Gizmos.DrawWireSphere(RingFocusDisplay.transform.position, OuterCursorRadius);
    }

    private void Update()
    {
        Vector3 mousePosWorld = Mouse.current.position.value;
        float len = math.distance(RingFocusDisplay.transform.position, mousePosWorld);

        var stepRot = 360f / Ring.k_RingCount;

        int curIndex = RingFocusDisplay.RingIndex;
        var curRotAng = curIndex * stepRot;

        var targetRotAng = -Vector3.SignedAngle(Vector3.up, mousePosWorld - RingFocusDisplay.transform.position, Vector3.forward);
        var targetIndex = ((int)math.round(targetRotAng / stepRot) + Ring.k_RingCount) % Ring.k_RingCount;

        var distScale = math.max(
            (InnerLeniencyRadius - len) / (3 * (InnerLeniencyRadius - InnerCursorRadius)),
            (len - OuterLeniencyRadius) / (3 * (OuterCursorRadius - OuterLeniencyRadius))
        );
        distScale = math.clamp(1.0f - distScale, 0, 1);

        var leanRotAng = Mathf.MoveTowardsAngle(curRotAng, targetRotAng, (stepRot / 2) * math.abs(Mathf.DeltaAngle(curRotAng, targetRotAng) / 180f) * distScale);
        Quaternion leanRot = Quaternion.Euler(180f + leanRotAng, 90, 90).normalized;

        RingFocusDisplay.Visual.localRotation = Quaternion.Slerp(RingFocusDisplay.Visual.localRotation, leanRot, Time.deltaTime * 10f);

        if (InnerLeniencyRadius < len && len < OuterLeniencyRadius)
        {
            // Snap to closest ring (plus lean)
            if (curIndex != targetIndex)
                RingFocusDisplay.UpdateRing(targetIndex, RingDisplays[targetIndex]);
        }
    }

    private void OnInteract()
    {
    }

    public void OnStateChanged(HandUIController.State oldState, HandUIController.State newState)
    {
        TransitionPoint target;
        switch (newState)
        {
            case HandUIController.State.Inventory:
                target = InventoryT;
                break;
            default:
            case HandUIController.State.Closed:
                target = ClosedT;
                break;
        }

        Co.StartCoroutine(this, target.Lerp((RectTransform)transform, HandUIController.k_AnimTransitionTime));
    }

    private void OnGameEvent(GameEvents.Data data)
    {
        var eType = data.Type; var entity = data.Entity;
        if (eType != GameEvents.Type.InventoryChanged) return;
        if (!GameEvents.TryGetSharedComponent<PlayerControlled>(entity, out var player)) return;
        if (player.Index != Game.ClientGame.PlayerIndex) return;
        if (GameEvents.TryGetBuffer<Ring>(entity, out var rings) && GameEvents.TryGetBuffer<EquippedGem>(entity, out var equippedGems))
        {
            // Update ring display
            for (int i = 0; i < rings.Length && i < RingDisplays.Length; i++)
            {
                // Get the 'subset' of gems that this ring uses
                var equippedGemsForRing = equippedGems.AsNativeArray().AsReadOnlySpan().Slice(i * Gem.k_GemsPerRing, Gem.k_GemsPerRing);
                RingDisplays[i].UpdateRing(i, rings[i], equippedGemsForRing);
                RingDisplays[i].SnapBackToOrigin();

                // Update the focus display if this ring is focused
                if (RingFocusDisplay.IsFocused(RingDisplays[i]) || RingFocusDisplay.IsFocused(null))
                    RingFocusDisplay.UpdateRing(i, RingDisplays[i]);
            }
        }

        if (GameEvents.TryGetBuffer<InventoryGem>(entity, out var gems))
        {
            // Update gem display
            HashSet<uint> toRemove = new(m_InventoryGems.Keys);
            for (int i = 0; i < gems.Length; i++)
            {
                toRemove.Remove(gems[i].Gem.ClientId);

                if (!m_InventoryGems.TryGetValue(gems[i].Gem.ClientId, out var gemDisplay))
                {
                    // Create a new display if it doesn't exist
                    gemDisplay = Instantiate(GemDisplayPrefab, GemDisplayPrefab.transform.parent);
                    m_InventoryGems[gems[i].Gem.ClientId] = gemDisplay;
                    gemDisplay.SnapBackToRandom();
                    gemDisplay.gameObject.SetActive(true);
                }
                else
                {
                    // Old ones SHOULDN'T need updating
                }

                gemDisplay.UpdateGem(i, gems[i].Gem);
            }

            // Remove any displays that are no longer in the inventory
            foreach (var remove in toRemove)
                if (m_InventoryGems.Remove(remove, out var gemDisplay))
                    Destroy(gemDisplay.gameObject);
        }
    }
}