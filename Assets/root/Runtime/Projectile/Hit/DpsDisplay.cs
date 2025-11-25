using System;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class DpsDisplay : AutoPoolBehaviour
{
    const int TARGET_DUMMY_HEALTH = 100;
    const float DamageNumberDuration = 1f;
    const float CountupNumberDuration = 2f;

    DpsDisplayManager _parent;
    
    Entity _entity;
    int _initHealth;
    int _currentHealth;
    
    const float TimeBetweenSnapshots = 0.1f;
    const float DpsCumulationDuration = 5;
    static int[] EmptySnapshotArray => new int[(int)(DpsCumulationDuration/TimeBetweenSnapshots)];
    
    int[] _snapshots = EmptySnapshotArray;
    int _snapshotIndex = 0;
    double _lastSnapshotTime;
    double _lastChangeTime;
    
    public CanvasGroup CanvasGroup;
    
    // Floating damage numbers
    public DamageNumber DamageNumberTemplate;
    
    // Health bar
    public Image HealthBarFill;
    public TMP_Text HealthBarText;
    
    // DPS text
    public TMP_Text DpsText;

    // Damage Countup Text
    public TMP_Text DamageCountupText;
    public DamageNumber DamageCountupTemplate;

    public override void NewObjectSetup()
    {
        
    }

    public void Setup(DpsDisplayManager parent, Entity entity, int initHealth, int currentHealth)
    {
        _parent = parent;
        _entity = entity;
        
        
        _initHealth = initHealth;
        _currentHealth = currentHealth;
        
        _snapshotIndex = 0;
        _lastSnapshotTime = _lastChangeTime = Time.timeAsDouble;
        Array.Fill(_snapshots, currentHealth);
    }

    public void AddHealth(int change)
    {
        _lastChangeTime = Time.timeAsDouble;
        _currentHealth += change;
        
        var cur = _initHealth == int.MaxValue ? (_currentHealth-_snapshots[0]) + TARGET_DUMMY_HEALTH : _currentHealth;
        var max = _initHealth == int.MaxValue ? TARGET_DUMMY_HEALTH : _initHealth;
        var fill = (float)cur / max;
        HealthBarFill.fillAmount = fill;
        HealthBarText.text = $"{cur}/{max}";
        HealthBarText.color = HealthBarFill.color = Color.Lerp(Palette.HealthChangeNegative, Palette.HealthChangePositive, ease.cubic(fill));
        
        DamageNumberTemplate.GetFromPool().Setup(transform, change, DamageNumberDuration);
    }

    private void Update()
    {
        var t = Time.timeAsDouble;
        var dps = (_currentHealth - _snapshots[(_snapshotIndex+1) % _snapshots.Length])/DpsCumulationDuration;
        DpsText.text = dps.ToString("N2") + "/s";
        
        while (_lastSnapshotTime + TimeBetweenSnapshots < t)
        {
            _lastSnapshotTime += TimeBetweenSnapshots;
            _snapshotIndex++;
            
            if (_snapshotIndex >= _snapshots.Length)
            {
                DamageCountupTemplate.GetFromPool().Setup(transform, _currentHealth-_snapshots[0], CountupNumberDuration);
                _snapshotIndex = 0;
            }
            _snapshots[_snapshotIndex] = _currentHealth;
        }
        DamageCountupText.text = (_currentHealth - _snapshots[0]).ToString();
        
        float fade = 1 - (float)(t - _lastChangeTime)/(DpsCumulationDuration*1.5f);
        CanvasGroup.alpha = math.clamp(fade*2, 0, 1);
        
        if (fade <= 0)
            _parent.Remove(_entity);
    }
}