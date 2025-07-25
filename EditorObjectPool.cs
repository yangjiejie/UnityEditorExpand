using System;
using System.Collections.Generic;

namespace UnityEditorPool
{
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Stack<T> _pool;
        private readonly Action<T> _resetAction; // 归还时可清理
        private readonly Action<T> _onetimeInitAction; // 首次创建时可定制
        private readonly int _maxPoolSize;

        public ObjectPool(int initialCapacity = 10, int maxPoolSize = 100,
            Action<T> resetAction = null,
            Action<T> onetimeInitAction = null)
        {
            _pool = new Stack<T>(initialCapacity);
            _maxPoolSize = maxPoolSize;
            _resetAction = resetAction;
            _onetimeInitAction = onetimeInitAction;
        }

        public T Get()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            var obj = new T();
            _onetimeInitAction?.Invoke(obj);
            return obj;
        }

        public void Release(T obj)
        {
            _resetAction?.Invoke(obj);
            if (_pool.Count < _maxPoolSize)
                _pool.Push(obj);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        public int Count => _pool.Count;
    }

    public static class ListPool<T>
    {
        private static readonly ObjectPool<List<T>> _pool =
            new ObjectPool<List<T>>(
                initialCapacity: 100,
                maxPoolSize: 200,
                resetAction: l => l.Clear()); // 注意reset的时候一定要clear

        public static List<T> Get()
        {
            return _pool.Get();
        }

        public static void Release(List<T> toRelease)
        {
            _pool.Release(toRelease);
        }

        public static void ClearAll()
        {
            _pool.Clear();
        }
    }

}
