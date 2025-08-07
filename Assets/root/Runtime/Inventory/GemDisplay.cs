using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class GemDisplay : DraggableElement
{
	public MeshRenderer Renderer;
    Gem m_Gem;

    public void UpdateGem(Gem gem)
    {
        m_Gem = gem;
        
        // TODO: Renderer.material = m_Gem.Material;
    }
}