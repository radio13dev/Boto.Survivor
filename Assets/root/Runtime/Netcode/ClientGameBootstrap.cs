using Unity.Entities;
using UnityEngine.Scripting;

[Preserve]
public class ClientGameBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        //Game.ClientGame = new Game(true);
        World.DefaultGameObjectInjectionWorld = new World("Empty");// Game.ClientGame.World;
        return true;
    }
}