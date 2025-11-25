using Unity.Entities;
using UnityEngine;

public class LobbyReadyChargeDisplay : MonoBehaviour
{
    public GameObject Visual;
    public MeshRenderer ChargeMaterial;

    private void Update()
    {
        Visual.SetActive(LobbyPresentationSystem.CurrentLobbyEntity != Entity.Null);
        if (Visual.activeSelf)
        {
            ChargeMaterial.material.SetFloat("_Fill", GameEvents.GetComponent<Circling>(CameraTarget.MainTarget.Entity).Percentage);
        }
    }
}