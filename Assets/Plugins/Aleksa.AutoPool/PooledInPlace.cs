using System;
using System.Collections.Generic;
using UnityEngine;

internal class PooledInPlace<T> where T : MonoBehaviour
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly List<T> pooledItems = new List<T>();
    
    internal PooledInPlace(T prefab)
    {
        this.prefab = prefab;
        this.parent = prefab.transform.parent;
        pooledItems.Add(prefab);
    }

    internal PooledInPlaceRefresher BeginUIUpdate()
    {
        return new PooledInPlaceRefresher(this);
    }
    
    
    internal struct PooledInPlaceRefresher : IDisposable
    {
        private readonly PooledInPlace<T> pool;
        private int currentIndex;

        internal PooledInPlaceRefresher(PooledInPlace<T> pool)
        {
            this.pool = pool;
            this.currentIndex = 0;
        }

        internal T Get()
        {
            T itemToReturn = default;
            if (currentIndex < pool.pooledItems.Count)
            {
                itemToReturn = pool.pooledItems[currentIndex];
            }
            else
            {
                itemToReturn = UnityEngine.Object.Instantiate(pool.prefab, pool.parent);
                pool.pooledItems.Add(itemToReturn);
            }

            itemToReturn.transform.SetAsLastSibling();
            currentIndex++;
            return itemToReturn;
        }

        public void Dispose()
        {
            for (int i = 0; i < currentIndex; i++)
            {
                var item = pool.pooledItems[i];
                if (item != null)
                {
                    item.gameObject.SetActive(true);
                }
            }
            
            for (int i = currentIndex; i < pool.pooledItems.Count; i++)
            {
                var item = pool.pooledItems[i];
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                }
            }
        }
    }
}