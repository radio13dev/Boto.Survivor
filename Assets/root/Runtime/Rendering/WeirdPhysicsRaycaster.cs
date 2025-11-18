using UnityEngine;
using UnityEngine.EventSystems;

public class WeirdPhysicsRaycaster : PhysicsRaycaster
{
    public override int sortOrderPriority => 2;
}