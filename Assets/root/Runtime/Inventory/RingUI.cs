using UnityEngine;

public class RingUI : MonoBehaviour
{
    
    private void OnEnable()
    {
        HandUIController.Attach(this);
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);
    }
}