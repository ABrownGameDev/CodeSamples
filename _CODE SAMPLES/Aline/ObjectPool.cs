namespace Interfaces
{
    using System.Collections.Generic;

    public abstract class ObjectPoolObject
    {
        public object owner;
        public abstract void Create(UnityEngine.GameObject go);

        /// <summary>
        /// Handle any necessary cleanup here
        /// </summary>
        public abstract void OnRelease();
        
        /// <summary>
        /// Handle any necessary activation/initialization here
        /// </summary>
        public abstract void OnRequest();
    }

    /// <summary>
    /// This class is designed to automatically pool and track gameObjects that derive from the ObjectPoolObject abstract class
    /// </summary>
    public class ObjectPool<T> where T : ObjectPoolObject, new()
    {
        private class PooledObject
        {
            public bool inUse = false;
            public T obj = new T();
#pragma warning disable 0649
            public UnityEngine.GameObject go;
#pragma warning restore 0649

            // Creates a new PooledObject, but delegates the creation of the gameObject to the ObjectPoolObject interface
            public PooledObject(object _owner, UnityEngine.GameObject _go = null)
            {
                obj.Create(_go);
                obj.owner = _owner;
                go = _go;
            }
        }

        private List<PooledObject> objects;

        public ObjectPool(object owner, int size = 0)
        {
            objects = new List<PooledObject>(size);
            objects.Add(new PooledObject(owner)); // Create the first, template object of the pool

            for (int i = 1; i < size; i++)
                objects.Add(new PooledObject(owner, objects[0].go)); // Use template to instantiate the rest
        }

        public T RequestObject(object owner)
        {
            // Attempts to find an already-existing object 
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].inUse == false)
                {
                    objects[i].obj.OnRequest();
                    objects[i].inUse = true;
                    objects[i].obj.owner = owner;
                    //UnityEngine.Debug.Log(string.Format("Returning pre-existing {0}!", objects[0].GetType(), objects[i]));
                    return objects[i].obj;
                }
            }
            
            // If that fails, creates a new object
            objects.Add(new PooledObject(owner, objects[0].go)); // Use template
            objects[objects.Count - 1].obj.OnRequest();
            objects[objects.Count - 1].inUse = true;
            objects[objects.Count - 1].obj.owner = owner;
            //UnityEngine.Debug.Log(string.Format("Creating new {0}!", objects[0].GetType()));
            return objects[objects.Count - 1].obj;
        }

        public void ReleaseObject(T obj)
        {
            for (int i = 0; i < objects.Count; i++)
                if (objects[i].obj == obj)
                {
                    objects[i].inUse = false;
                    objects[i].obj.OnRelease();
                }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < objects.Count; i++)
            {
                objects[i].inUse = false;
                objects[i].obj.OnRelease();
            }
        }

        public void Poll()
        {
            UnityEngine.Debug.LogWarning(string.Format("Object pool of type {0} contains {1} objects.", typeof(T), objects.Count));
            //UnityEngine.Debug.LogWarning(string.Format("Object pool of type {0} contains {1} more objects than it did upon creation.", typeof(T), InUse() - creationSize));
        }

        public int Count()
        {
            return objects.Count;
        }

        private int InUse()
        {
            return objects.FindAll(x => x.inUse).Count;
        }
    }
}