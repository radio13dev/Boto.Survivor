using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ProfileGameObjects : MonoBehaviour
{
    public GameObject Template;
    List<GameObject> m_Spawned = new();

    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
    
    private void OnEnable()
    {
        for (int i = 0; i < Profiling.k_ProfileCount; i++)
        {
            var p = new Vector3(Random.Range(-100, 100), Random.Range(-100, 100), 0);
            var q = Random.rotation;
            m_Spawned.Add(Instantiate(Template, p, q, Template.transform.parent));
            m_Spawned[^1].SetActive(true);
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < m_Spawned.Count; i++)
            Destroy(m_Spawned[i]);
        m_Spawned.Clear();
    }
}