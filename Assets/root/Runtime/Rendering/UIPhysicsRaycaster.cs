using UnityEngine;
using UnityEngine.EventSystems;

public class UIPhysicsRaycaster : PhysicsRaycaster
{
    public override int sortOrderPriority => 1;
}