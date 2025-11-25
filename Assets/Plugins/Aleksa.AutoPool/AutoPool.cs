using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

//Only a single autoPoolBehaviour can be attached to a prefab.
[DisallowMultipleComponent]
public abstract class AutoPoolBehaviour : MonoBehaviour
{
    [HideInInspector]
    public GameObject PoolKey; 
    public abstract void NewObjectSetup();
    
    public void SetupPoolSource(GameObject poolSource)
    {
        if (poolSource == null)
        {
            throw new Exception($"Prefab component cannot be null.");
        }
        
        PoolKey = poolSource;
    }
}

public static class IAutoPoolExtensions
{
    public static T GetFromPool<T>(this T prefabComponent) where T : AutoPoolBehaviour
    {
        return AutoPool.Get(prefabComponent);
    }
    
    public static void ReturnToPool<T>(this T poolObject, float afterSeconds = 0) where T : AutoPoolBehaviour
    {
        if (afterSeconds <= 0)
            AutoPool.Return(poolObject);
        else
        {
            CoroutineHost.Instance.StartCoroutine(DelayedReturnToPool(poolObject, afterSeconds));
        }
    }
    
    static IEnumerator DelayedReturnToPool<T>(T poolObject, float afterSeconds) where T : AutoPoolBehaviour
    {
        yield return new WaitForSeconds(afterSeconds);
        AutoPool.Return(poolObject);
    }
    
    public static int GetPoolActiveCount<T>(this T prefabComponent) where T : AutoPoolBehaviour
    {
        return AutoPool.GetActiveCount(prefabComponent);
    }
}

public static class AutoPool
{
    class AutoPoolContainer
    {
        public GameObject prefab;
        public Stack<object> pool = new Stack<object>();
        public int active;

        public bool TryPopT<T>(out T returnObject) where T : AutoPoolBehaviour
        {
            if (pool.TryPeek(out var objectResult))
            {
                if (objectResult is T typedObject)
                {
                    pool.Pop();
                    returnObject = typedObject;
                    return true;
                }
            }
            
            returnObject = null;
            return false;
        }
    }
    
    private static Dictionary<GameObject, AutoPoolContainer> pools = new Dictionary<GameObject, AutoPoolContainer>();
    public static T Get<T>(T prefabComponent) where T : AutoPoolBehaviour
    {
        if (prefabComponent == null)
        {
            throw new Exception($"Prefab component cannot be null.");
        }
        
        var poolKey = prefabComponent.gameObject;
        if (!pools.TryGetValue(poolKey, out var genericPool))
        {
            var newPool = new AutoPoolContainer
            {
                prefab = prefabComponent.gameObject
            };
            pools[poolKey] = newPool;
            genericPool = newPool;
        }
        
        if (!genericPool.TryPopT<T>(out var poolObject))
        {
            var newObject = Object.Instantiate(genericPool.prefab);
            poolObject = newObject.GetComponent<T>();
            poolObject.SetupPoolSource(poolKey);
        }

        poolObject.gameObject.SetActive(true);
        poolObject.NewObjectSetup();
        genericPool.active++;
        return poolObject;
    }
    
    public static void Return(AutoPoolBehaviour poolObject)
    {
        if (poolObject == null)
        {
            Debug.LogError($"Pool object cannot be null.");
            return;
        }
        
        var poolKey = poolObject.PoolKey;
        if (!pools.TryGetValue(poolKey, out var genericPool))
        {
            throw new Exception($"Where's the pool for {poolKey}!?");
        }
        
        poolObject.gameObject.SetActive(false);
        genericPool.pool.Push(poolObject);
        genericPool.active--;
    }
    
    public static int GetActiveCount<T>(T prefabComponent) where T : AutoPoolBehaviour
    {
        if (prefabComponent == null)
        {
            throw new Exception($"Prefab component cannot be null.");
        }
        
        var poolKey = prefabComponent.gameObject;
        if (!pools.TryGetValue(poolKey, out var genericPool))
        {
            var newPool = new AutoPoolContainer
            {
                prefab = prefabComponent.gameObject
            };
            pools[poolKey] = newPool;
            genericPool = newPool;
        }
        return genericPool.active;
    }
}
