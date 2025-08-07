using System;
using UnityEngine;

public class ElementFocus : MonoBehaviour
{
    public Transform Visual;
    public float Speed = 10f;
    public Vector3 FocusedScale = new Vector3(0.4f,0.4f,0.4f);
    public float FocusScaleSpeed = 10f;
    bool m_Active = false;

    private void Update()
    {
        var visualT = Visual.gameObject.transform;
        if (UIFocus.Interact && !UIFocus.Focus)
        {
            transform.position = Vector3.Lerp(transform.position, UIFocus.Interact.transform.position, Time.deltaTime*Speed);
            visualT.localScale = Vector3.Lerp(visualT.localScale, FocusedScale, Time.deltaTime*FocusScaleSpeed);
        }
        else if (UIFocus.Focus)
        {
            if (!m_Active)
            {
                m_Active = true;
                transform.position = UIFocus.Focus.transform.position;
            }
            transform.position = Vector3.Lerp(transform.position, UIFocus.Focus.transform.position, Time.deltaTime*Speed);
            visualT.localScale = Vector3.Lerp(visualT.localScale, Vector3.one, Time.deltaTime*FocusScaleSpeed);
        }
        else
        {
            visualT.localScale = Vector3.Lerp(visualT.localScale, Vector3.zero, Time.deltaTime*FocusScaleSpeed);
            m_Active = false;
        }
    }
}
