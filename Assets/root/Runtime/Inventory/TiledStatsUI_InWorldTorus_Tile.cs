using AYellowpaper.SerializedCollections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TiledStatsUI_InWorldTorus_Tile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public MeshFilter[] MainMeshes;
    public MeshFilter[] MainOutlineMeshes;
    public MeshRenderer[] MainRenderers;
    public SpriteRenderer[] MainIcons;
    public SpriteRenderer[] MainColoredSprites;
    public MeshCollider InteractCollider;
    public GameObject[] HoveredVisuals;
    
    public Material Material;
    public Material MaterialDisabled;
    public Material MaterialPurchased;
    
    public Animator Animator;
    
    public SerializedDictionary<TiledStatsUI_InWorldTorus.eState, GameObject[]> StateVisuals = new();
    
    public void SetMesh(Mesh mesh)
    {
        InteractCollider.sharedMesh = mesh;
        for (int i = 0; i < MainMeshes.Length; i++)
        {
            MainMeshes[i].sharedMesh = mesh;
        }
    }
    public void SetOutlineMesh(Mesh mesh)
    {
        for (int i = 0; i < MainOutlineMeshes.Length; i++)
        {
            MainOutlineMeshes[i].sharedMesh = mesh;
        }
    }
    
    public void SetMaterials(Material material, Material materialDisabled, Material materialPurchased)
    {
        Material = material;
        MaterialDisabled = materialDisabled;
        MaterialPurchased = materialPurchased;
    }
    
    public void SetSprite(Sprite sprite)
    {
        for (int i = 0; i < MainIcons.Length; i++)
        {
            MainIcons[i].sprite = sprite;
        }
    }

    public void SetUnlocked(TiledStatsUI_InWorldTorus.eState state)
    {
        // Disable all
        foreach (var kvp in StateVisuals)
        {
            for (int i = 0; i < kvp.Value.Length; i++)
            {
                kvp.Value[i].SetActive(false);
            }
        }
        // Enable required
        if (StateVisuals.TryGetValue(state, out var visuals))
        {
            for (int i = 0; i < visuals.Length; i++)
            {
                visuals[i].SetActive(true);
            }
        }
        
        for (int i = 0; i < MainRenderers.Length; i++)
            MainRenderers[i].sharedMaterial = Material;
        MainRenderers[0].sharedMaterial = state == TiledStatsUI_InWorldTorus.eState.Purchased ? Material : MaterialDisabled;
        MainRenderers[1].sharedMaterial = state == TiledStatsUI_InWorldTorus.eState.Purchased ? Material : MaterialDisabled;
        MainRenderers[2].sharedMaterial = MaterialPurchased;
        MainRenderers[3].sharedMaterial = MaterialDisabled;
        
        if (MainRenderers.Length == 0) return;
        var col = Material.GetColor("_Dither_ColorA");
        if (state == TiledStatsUI_InWorldTorus.eState.Locked)
        {
            col = col * new Color(0.5f, 0.5f, 0.5f, 0);
        }
        else if (state == TiledStatsUI_InWorldTorus.eState.Available)
        {
            col = col * new Color(1,1,1,0);
        }
        else if (state == TiledStatsUI_InWorldTorus.eState.Purchased)
        {
            col = col;
        }
        
        for (int i = 0; i < MainColoredSprites.Length; i++)
        {
            MainColoredSprites[i].color = col;
        }
        
        Animator.SetInteger("State", (int)state);
    }
    
    [EditorButton]
    public void DemoLocked() => SetUnlocked(TiledStatsUI_InWorldTorus.eState.Locked);
    [EditorButton]
    public void DemoAvailable() => SetUnlocked(TiledStatsUI_InWorldTorus.eState.Available);
    [EditorButton]
    public void DemoPurchased() => SetUnlocked(TiledStatsUI_InWorldTorus.eState.Purchased);


    [EditorButton]
    public void DemoHovered()
    {
        for (int i = 0; i < HoveredVisuals.Length; i++)
        {
            HoveredVisuals[i].SetActive(true);
        }
        Animator.SetBool("Hovered", true);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
    }

    [EditorButton]
    public void DemoUnhovered()
    {
        for (int i = 0; i < HoveredVisuals.Length; i++)
        {
            HoveredVisuals[i].SetActive(false);
        }
        Animator.SetBool("Hovered", false);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
    }
}