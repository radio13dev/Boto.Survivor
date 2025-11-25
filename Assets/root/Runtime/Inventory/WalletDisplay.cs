using System.Collections;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class WalletDisplay : MonoBehaviour
{
    public RotateOverTime Spinner;
    public TMP_Text Value;
    public Transform ChangeContainer;
    public TMP_Text Change;
    public GameObject PositiveChange;
    public GameObject NegativeChange;
    
    long _lastValue;
    long _displayed;
    long _displayedChange;
    ExclusiveCoroutine _valueChangeCo;
    ExclusiveCoroutine _changeDisplayCo;
    
    private void OnEnable()
    {
        
        HandUIController.Attach(this);

        GameEvents.OnWalletChanged += OnWalletChanged;
        if (GameEvents.TryGetComponent2(CameraTarget.MainEntity, out Wallet wallet)) OnWalletChanged(CameraTarget.MainEntity, wallet);
        else SetValue(0);
    }

    private void OnDisable()
    {
        HandUIController.Detach(this);

        GameEvents.OnWalletChanged -= OnWalletChanged;
    }

    private void OnWalletChanged(Entity entity, Wallet wallet)
    {
        if (entity != CameraTarget.MainEntity) return;
        SetValue(wallet.Value);
    }

    
    private void SetValue(long walletValue)
    {
        _valueChangeCo.StartCoroutine(this, AnimateTowards(walletValue));
    }

    private IEnumerator AnimateTowards(long walletValue)
    {
        _changeDisplayCo.StartCoroutine(this, ShowChange(walletValue - _lastValue));
    
        _lastValue = walletValue;
        const float tickChangeDuration = 0.1f;
        float tickChangeT = 0;
        while (_displayed != walletValue)
        {
            var dif = walletValue - _displayed;
            _displayed += (long)((walletValue - _displayed)*Time.deltaTime);
            _displayed += (long)(math.sign(dif)*math.min(math.abs(dif), 1));
            
            tickChangeT += Time.deltaTime;
            while (tickChangeT > tickChangeDuration)
            {
                tickChangeT -= tickChangeDuration;
                _displayed += (long)(math.sign(dif)*math.min(math.abs(dif), 1));
            }
            Value.text = _displayed.ToGemString();
            yield return null;
        }
        
        // Finalize
        _lastValue = walletValue;
        _displayed = walletValue;
        Value.text = walletValue.ToGemString();
    }

    private IEnumerator ShowChange(long change)
    {
        _displayedChange += change;
        
        // Update displayed value, color and 'change' icon
        if (_displayedChange == 0)
        {
            ChangeContainer.gameObject.SetActive(false);
            yield break;
        }
        
        Spinner.Spin();
        
        var origColor = Change.color = _displayedChange >= 0 ? Palette.MoneyChangePositive : Palette.MoneyChangeNegative;
        ChangeContainer.gameObject.SetActive(true);
        ChangeContainer.localPosition = math.float3(Random.insideUnitCircle*5, 0);
        ChangeContainer.localRotation = Quaternion.Euler(0,0, Mathf.LerpAngle(-10, 10, Random.value));
        Change.text = _displayedChange.ToGemChangeString();
        PositiveChange.gameObject.SetActive(_displayedChange >= 0);
        NegativeChange.gameObject.SetActive(_displayedChange < 0);
        //_displayedChange = 0;
        
        // Make number 'expand' quickly as the change applies
        float expandDuration = 0.15f;
        float expandT = 0;
        while (expandT < expandDuration)
        {
            expandT += Time.deltaTime;
            expandT = math.min(expandT, expandDuration);
            float scale = 1 + Mathf.Sin((expandT / expandDuration) * Mathf.PI) * 0.5f;
            ChangeContainer.localScale = new Vector3(scale, scale, scale);
            yield return null;
        }
        
        // now linger for a moment
        float lingerDuration = 0.5f;
        float lingerT = 0;
        while (lingerT < lingerDuration)
        {
            lingerT += Time.deltaTime;
            lingerT = math.min(lingerT, lingerDuration);
            ChangeContainer.localScale = Vector3.one;
            yield return null;
        }
        
        // fade out
        float fadeDuration = 0.2f;
        float fadeT = 0;
        while (fadeT < fadeDuration)
        {
            fadeT += Time.deltaTime;
            fadeT = math.min(fadeT, fadeDuration);
            var color = origColor;
            color.a = 1 - (fadeT / fadeDuration);
            ChangeContainer.localScale = Vector3.one*color.a;
            Change.color = color;
            yield return null;
        }
        
        // Finally, disable.
        ChangeContainer.gameObject.SetActive(false);
        _displayedChange = 0;
    }
}
