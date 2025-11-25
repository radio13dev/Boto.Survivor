using UnityEngine;
using UnityEngine.EventSystems;

public class TorusWorldPhysicsRaycaster : PhysicsRaycaster
{
    public override int sortOrderPriority => 0;
}
