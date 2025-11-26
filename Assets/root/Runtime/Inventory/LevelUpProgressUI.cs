using TMPro;
using Unity.Entities;
using UnityEngine;

public class LevelUpProgressUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _maxProgressText;
    [SerializeField] private TextMeshProUGUI _currentLevelText;
    [SerializeField] private PooledParticle _levelUpParticle;
    [SerializeField] private Transform _particlePosition;
    [SerializeField] private float _particleSize = 100f;

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
        else
        {
            _progressText.text = "0";
            _maxProgressText.text = "0";
            _currentLevelText.text = "0";
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
        _progressText.text = playerLevel.Progress >= playerLevel.LevelUpCost ? "0" : playerLevel.Progress.ToString();
    }

    private void OnPlayerLevelUp(Entity entity, PlayerLevel playerLevel)
    {
        if (!_maxProgressText || !_currentLevelText) { return; }
        _maxProgressText.text = playerLevel.LevelUpCost.ToString();
        _currentLevelText.text = playerLevel.Level.ToString();
        
        var particle = _levelUpParticle.GetFromPool();
        particle.transform.position = _particlePosition.position;
        particle.transform.rotation = _particlePosition.rotation;
        particle.transform.localScale = Vector3.one * _particleSize;
    }
}