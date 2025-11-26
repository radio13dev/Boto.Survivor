using TMPro;
using Unity.Entities;
using UnityEngine;

public class LevelUpProgressUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _maxProgressText;
    [SerializeField] private TextMeshProUGUI _currentLevelText;

    private void OnEnable()
    {
        HandUIController.Attach(this);
        
        GameEvents.OnPlayerLevelProgress += OnPlayerLevelProgress;
        GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;

        if (GameEvents.TryGetComponent2(CameraTarget.MainEntity, out PlayerLevel playerLevel))
        {
            OnPlayerLevelUp(CameraTarget.MainEntity, playerLevel);
            OnPlayerLevelProgress(CameraTarget.MainEntity, playerLevel);
        }
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);
        
        GameEvents.OnPlayerLevelProgress -= OnPlayerLevelProgress;
        GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;
    }

    private void OnPlayerLevelProgress(Entity entity, PlayerLevel playerLevel)
    {
        if (!_progressText) { return; }
        _progressText.text = playerLevel.Progress.ToString();
    }

    private void OnPlayerLevelUp(Entity entity, PlayerLevel playerLevel)
    {
        if (!_maxProgressText || !_currentLevelText) { return; }
        _maxProgressText.text = playerLevel.LevelUpCost.ToString();
        _currentLevelText.text = playerLevel.Level.ToString();
    }
}