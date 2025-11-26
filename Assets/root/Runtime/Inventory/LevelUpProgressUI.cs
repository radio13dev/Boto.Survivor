using System;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpProgressUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private TextMeshProUGUI _maxProgressText;
    [SerializeField] private TextMeshProUGUI _currentLevelText;
    
    [SerializeField] private Image _progressBarOutline;
    [SerializeField] private Image _progressBarBackground;
    [SerializeField] private Image _progressBarFill;
    
    [SerializeField] private PooledParticle _levelUpParticle;
    [SerializeField] private Transform _particlePosition;
    [SerializeField] private float _particleSize = 100f;
    
    private static readonly int StartAngleProperty = Shader.PropertyToID("_StartAngle");
    private static readonly int EndAngleProperty = Shader.PropertyToID("_EndAngle");
    private float _progressBarStartAngle;
    private float _progressBarEndAngle;
    
    private void OnEnable()
    {
        HandUIController.Attach(this);
        
        GameEvents.OnPlayerLevelProgress += OnPlayerLevelProgress;
        GameEvents.OnPlayerLevelUp += OnPlayerLevelUp;

        if (_progressBarBackground != null)
        {
            _progressBarStartAngle = _progressBarBackground.material.GetFloat(StartAngleProperty);
            _progressBarEndAngle = _progressBarBackground.material.GetFloat(EndAngleProperty);
        }
        
        SetProgressBarFill(0f);

        if (GameEvents.TryGetComponent2(CameraTarget.MainEntity, out PlayerLevel playerLevel))
        {
            OnPlayerLevelUp(CameraTarget.MainEntity, playerLevel);
            OnPlayerLevelProgress(CameraTarget.MainEntity, playerLevel);
        }
        else
        {
            OnPlayerLevelUp(CameraTarget.MainEntity, new PlayerLevel());
            OnPlayerLevelProgress(CameraTarget.MainEntity, new PlayerLevel());
        }
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);
        
        GameEvents.OnPlayerLevelProgress -= OnPlayerLevelProgress;
        GameEvents.OnPlayerLevelUp -= OnPlayerLevelUp;

        if (_progressBarFill) { SetProgressBarFill(0f);}
    }

    private void OnPlayerLevelProgress(Entity entity, PlayerLevel playerLevel)
    {
        if (!_progressText) { return; }
        int playerProgress = playerLevel.Progress >= playerLevel.LevelUpCost ? 0 : playerLevel.Progress;
        _progressText.text = playerProgress.ToString();
        
        SetProgressBarFill((float) playerProgress / playerLevel.LevelUpCost);
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

    private void SetProgressBarFill(float percentage)
    {
        if (!_progressBarFill) { return; }
                
        float fillEndAngle = Mathf.Abs(_progressBarEndAngle - _progressBarStartAngle);
        fillEndAngle *= percentage;
        fillEndAngle += _progressBarStartAngle;
        _progressBarFill.material.SetFloat(EndAngleProperty, fillEndAngle);
    }
}