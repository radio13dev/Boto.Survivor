using UnityEngine;

public class GemDisplay : MonoBehaviour
{
	public MeshRenderer Renderer;
    Gem m_Gem;

    public void UpdateGem(Gem gem)
    {
        m_Gem = gem;
        
        // TODO: Renderer.material = m_Gem.Material;
    }
}