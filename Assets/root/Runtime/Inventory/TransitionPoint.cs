using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public struct TransitionPoint : IEquatable<TransitionPoint>
{
    [HideInInspector] [SerializeField] public Vector2 _anchorMin;
    [HideInInspector] [SerializeField] public Vector2 _anchorMax;
    [HideInInspector] [SerializeField] public Vector2 _anchoredPosition;
    [HideInInspector] [SerializeField] public Vector2 _sizeDelta;
    [HideInInspector] [SerializeField] public Vector2 _pivot;
    [HideInInspector] [SerializeField] public Quaternion _localRotation;
    [HideInInspector] [SerializeField] public Vector3 _localScale;

    public void Save(RectTransform t)
    {
        _anchorMin = t.anchorMin;
        _anchorMax = t.anchorMax;
        _anchoredPosition = t.anchoredPosition;
        _sizeDelta = t.sizeDelta;
        _pivot = t.pivot;

        _localRotation = t.localRotation;
        _localScale = t.localScale;
    }

    private void Apply(RectTransform target)
    {
        target.anchorMin = _anchorMin;
        target.anchorMax = _anchorMax;
        target.anchoredPosition = _anchoredPosition;
        target.sizeDelta = _sizeDelta;
        target.pivot = _pivot;

        target.localRotation = _localRotation;
        target.localScale = _localScale;
    }

    public static TransitionPoint Lerp(TransitionPoint from, TransitionPoint to, float t)
    {
        TransitionPoint final = new();

        final._anchorMin = math.lerp(from._anchorMin, to._anchorMin, t);
        final._anchorMax = math.lerp(from._anchorMax, to._anchorMax, t);
        final._anchoredPosition = math.lerp(from._anchoredPosition, to._anchoredPosition, t);
        final._sizeDelta = math.lerp(from._sizeDelta, to._sizeDelta, t);
        final._pivot = math.lerp(from._pivot, to._pivot, t);

        final._localRotation = math.slerp(from._localRotation, to._localRotation, t);
        final._localScale = math.lerp(from._localScale, to._localScale, t);

        return final;
    }

    public IEnumerator Lerp(RectTransform from, float duration, ease.Mode easeMode = ease.Mode.elastic_out)
    {
        TransitionPoint start = new();
        start.Save(from);

        float t = 0;
#if UNITY_EDITOR
        var t0 = EditorApplication.timeSinceStartup;
#endif
        while (t < duration && from)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                t = (float)(EditorApplication.timeSinceStartup - t0);
            else
#endif
                t += Time.deltaTime;

            t = math.min(t, duration);

            // Smooth the progress so it eases in and out
            float progress = t / duration;
            progress = easeMode.Evaluate(progress);// CoroutineHost.Methods.EaseCubic(progress);

            var part = TransitionPoint.Lerp(start, this, progress);
            part.Apply(from);
            yield return null;
        }
    }

    public bool Equals(RectTransform rect)
    {
        var comp = new TransitionPoint();
        comp.Save(rect);
        return this.Equals(comp);
    }

    public bool Equals(TransitionPoint other)
    {
        return _anchorMin.Equals(other._anchorMin) && _anchorMax.Equals(other._anchorMax) && _anchoredPosition.Equals(other._anchoredPosition) &&
               _sizeDelta.Equals(other._sizeDelta) &&
               _pivot.Equals(other._pivot) &&
               _localRotation.Equals(other._localRotation) && _localScale.Equals(other._localScale);
    }

    public override bool Equals(object obj)
    {
        return obj is TransitionPoint other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_anchorMin, _anchorMax, _anchoredPosition, _sizeDelta, _pivot, _localRotation, _localScale);
    }

    public static bool operator ==(TransitionPoint left, TransitionPoint right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TransitionPoint left, TransitionPoint right)
    {
        return !left.Equals(right);
    }
}