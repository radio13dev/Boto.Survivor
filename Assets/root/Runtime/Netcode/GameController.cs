using System;
using Unity.Core;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [EditorButton]
    public void ToggleAutoUpdate()
    {
        enabled = !enabled;
    }
    
    [EditorButton]
    public void NextFrame()
    {
        Update();
    }

    private void Update()
    {
        Game.World.Update();
    }
}