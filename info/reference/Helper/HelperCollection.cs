using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Random = System.Random;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace System.Collections.Generic {
    /*public class PriorityQueue<T>
    {
        private int _size;
        public int Size
        {
            get
            {
                if (_size < 0) _size = 0;
                return _size;
            }
            set => _size = value;
        }

        private int _capacity;
        public int Capacity
        {
            get
            {
                if (_capacity < 0) _capacity = 0;
                return _capacity;
            }
            set => _capacity = value;
        }

        public T[] _elements;

        public bool IsEmpty => _size == 0;
        public T Top => _elements[0];

        public int Count => _size;

        private readonly IComparer<T> _comparator;

        public PriorityQueue(IComparer<T> comparator, int capacity = 1)
        {
            Size = 0;
            Capacity = capacity;
            _elements = new T[capacity];
            _comparator = comparator;
        }

        public void Enqueue(T element)
        {
            if (Size == Capacity)
            {
                ExpandCapacity();
            }

            _elements[Size] = element;
            HeapInsert(_elements, Size);
            Size++;
        }

        public T Dequeue()
        {
            if (Size == 0)
            {
                return default(T);
            }

            T element = _elements[0];
            Swap(_elements, 0, Size - 1);
            Size--;
            Heapify(_elements, 0, Size);
            return element;
        }

        public T Peek()
        {
            return Top;
        }

        private void HeapInsert(T[] elements, int index)
        {
            //比较当前节点与父节点之间的大小，从插入位置向上处理。
            while (_comparator.Compare(elements[index], elements[(index - 1) / 2]) > 0)
            {
                Swap(elements, index, (index - 1) / 2);
                index = (index - 1) / 2;
            }
        }

        public void Clear()
        {
            Size = 0;
        }

        private void Heapify(T[] elements, int index, int size)
        {
            int left = index * 2 + 1;
            while (left < size)
            {
                int comparatorNum = left + 1 < size && _comparator.Compare(elements[left + 1], elements[left]) > 0 ? left + 1 : left;
                comparatorNum = _comparator.Compare(elements[comparatorNum], elements[index]) > 0 ? comparatorNum : index;
                if (comparatorNum == index)
                {
                    break;
                }
                Swap(elements, comparatorNum, index);
                index = comparatorNum;
                left = index * 2 + 1;
            }
        }

        private void ExpandCapacity()
        {
            Capacity = Mathf.CeilToInt(Capacity * 1.5f);
            T[] newElements = new T[Capacity];
            for (int i = 0; i < _elements.Length; i++)
            {
                newElements[i] = _elements[i];
            }
            _elements = newElements;
        }

        private void Swap(T[] elements, int i, int j)
        {
            (elements[i], elements[j]) = (elements[j], elements[i]);
        }
    }*/

    public class PriorityQueue<T>
    {
        private int _size;
        public int Size { get { return _size; } set { _size = value; } }

        private int _capacity;
        public int Capacity { get { return _capacity; } set { _capacity = value; } }

        private QueueItem[] _elements;

        public int Count => _size;

        public bool IsEmpty => _size == 0;
        public T Top => _elements[0].Element;

        private readonly IComparer<T> _comparator;
        private int _sequenceNumber = 0;

        private class QueueItem
        {
            public T Element { get; set; }
            public int SequenceNumber { get; set; }

            public QueueItem(T element, int sequenceNumber)
            {
                Element = element;
                SequenceNumber = sequenceNumber;
            }
        }

        public PriorityQueue(IComparer<T> comparator, int capacity = 10)
        {
            _size = 0;
            _capacity = capacity;
            _elements = new QueueItem[_capacity];
            _comparator = comparator;
        }

        public void Enqueue(T element)
        {
            if (_size == _capacity) ExpandCapacity();

            var newItem = new QueueItem(element, _sequenceNumber++);
            _elements[_size] = newItem;
            HeapInsert(_size++);
        }

        public T Dequeue()
        {
            if (_size == 0) return default(T);

            var element = _elements[0].Element;
            Swap(0, --_size);
            Heapify(0);
            return element;
        }

        public T Peek() => _elements[0].Element;

        private void HeapInsert(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_comparator.Compare(_elements[index].Element, _elements[parentIndex].Element) > 0 ||
                    (_comparator.Compare(_elements[index].Element, _elements[parentIndex].Element) == 0 &&
                     _elements[index].SequenceNumber < _elements[parentIndex].SequenceNumber))
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                }
                else
                {
                    break;
                }
            }
        }

        private void Heapify(int index)
        {
            int left, right, largest;

            while (true)
            {
                left = 2 * index + 1;
                right = 2 * index + 2;
                largest = index;

                if (left < _size && (_comparator.Compare(_elements[left].Element, _elements[largest].Element) > 0 ||
                                     (_comparator.Compare(_elements[left].Element, _elements[largest].Element) == 0 &&
                                      _elements[left].SequenceNumber < _elements[largest].SequenceNumber)))
                {
                    largest = left;
                }

                if (right < _size && (_comparator.Compare(_elements[right].Element, _elements[largest].Element) > 0 ||
                                      (_comparator.Compare(_elements[right].Element, _elements[largest].Element) == 0 &&
                                       _elements[right].SequenceNumber < _elements[largest].SequenceNumber)))
                {
                    largest = right;
                }

                if (largest != index)
                {
                    Swap(index, largest);
                    index = largest;
                }
                else
                {
                    break;
                }
            }
        }

        private void ExpandCapacity()
        {
            Capacity = Mathf.CeilToInt(Capacity * 1.5f);
            QueueItem[] newElements = new QueueItem[Capacity];
            for (int i = 0; i < _size; i++)
            {
                newElements[i] = _elements[i];
            }
            _elements = newElements;
        }

        private void Swap(int i, int j)
        {
            var temp = _elements[i];
            _elements[i] = _elements[j];
            _elements[j] = temp;
        }

    }

    public static class Dictionary {
        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultReturn = default(TValue)) { 
            if (dict.ContainsKey(key))
                return dict[key];
            else
                return defaultReturn;
        }
        
        public static void Set<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value) {
            if (dict.ContainsKey(key)) {
                dict[key] = value;
            } else {
                dict.Add(key, value);
            }
        }
    }

    public static class List {
        public static bool IsNullOrEmpty<T>(this IList<T> list) {
            return (list == null) || (list.Count == 0);
        }

        public static T Random<T>(this List<T> values) {
            if (values.Count == 0)
                return default(T);

            int index = UnityEngine.Random.Range(0, values.Count);
            return values[index];
        }

        public static List<T> Random<T>(this List<T> values, int count, bool repeat = true) {
            List<T> result = new List<T>();
            if (values.Count < count)
                return values.ToList();

            while (result.Count < count) {
                T rng = values.Random();
                if (!repeat && result.Contains(rng))
                    continue;

                result.Add(rng);
            }
            return result;
        }
        public static List<T> RandomAttribute<T,TKey>(this List<T> values, int count, Func<T, TKey> attribute) {
            List<T> tempValues = values;
            List<T> result = new List<T>();

            int uniqueAttributesCount = values.Select(attribute).Distinct().Count();
            count = Math.Min(count, uniqueAttributesCount);

            HashSet<TKey> selectedAttributes = new HashSet<TKey>();

            while (result.Count < count)
            {
                T rng = tempValues.Random();
                TKey selectedAttribute = attribute(rng);
                tempValues.Remove(rng);
                if (!selectedAttributes.Contains(selectedAttribute))
                {
                    selectedAttributes.Add(selectedAttribute);
                    result.Add(rng);
                }
            }
            return result;
        }

        public static int IndexOf<T>(this IEnumerable<T> values, T val) {
            var idx = values.AllIndexOf(val);
            return idx.Length == 0 ? -1 : idx.Min();
        }

        public static int FindIndex<T>(this IEnumerable<T> values, Predicate<T> pred) {
            var idx = values.FindAllIndex(pred);
            return idx.Length == 0 ? -1 : idx.Min();
        }

        public static int[] AllIndexOf<T>(this IEnumerable<T> values, T val) {
            Predicate<T> equal = x => object.Equals(x, val);
            return values.FindAllIndex(equal);
        }

        public static int[] FindAllIndex<T>(this IEnumerable<T> values, Predicate<T> pred) {
            return values.Select((x, i) => pred(x) ? i : -1).Where(i => i != -1).ToArray();
        }

        /// <summary>
        /// Add if not exists. Remove if exists.
        /// </summary>
        public static void Fluctuate<T>(this IList<T> list, T item) {
            if (list.Contains(item)) {
                list.Remove(item);
            } else {
                list.Add(item);
            }
        }
    
        public static void Swap<T>(this IList<T> list, int posA, int posB) {
            T value = list[posA];
            list[posA] = list[posB];
            list[posB] = value;
        }

        public static IList<T> Update<T>(this IList<T> list, T oldItem, T newItem) {
            return list.Update(list.IndexOf(oldItem), newItem);
        }

        public static IList<T> Update<T>(this IList<T> list, int oldIndex, T newItem) {
            if (!oldIndex.IsInRange(0, list.Count))
                return list;
        
            list[oldIndex] = newItem;
            return list;
        }

        public static T[] SubArray<T>(this T[] array, int start, int length = -1) {
            length = (length == -1) ? (array.Length - start) : length;
            T[] result = new T[length];
            Array.Copy(array, start, result, 0, length);
            return result;
        }
    
        /// <summary>
        /// Stop Exclusive.
        /// </summary>
        public static List<T> GetRange<T>(this IList<T> list, int start, int stop) {
            List<T> ret = new List<T>();
            start = Mathf.Max(0, start); 
            stop = Mathf.Min(stop, list.Count);

            if (start <= stop)
                return ret;

            for(int i = start; i < stop; i++) {
                ret.Add(list[i]);
            }
            return ret;
        }
    
        public static void RemoveAt<T>(this IList<T> lhs, int index) {
            lhs.RemoveAt(index);
        }
    
        public static void RemoveRange<T>(this IList<T> lhs, IList<T> rhs) {
            for(int i = 0; i < rhs.Count; i++) {
                lhs.Remove(rhs[i]);
            }
        }

        /*public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int n) {
            return source.Skip(Math.Max(0, source.Count() - n));
        }*/

        public static void GetModifiedList<T>(this IList<T> current, IList<T> target, ModifyOption option = ModifyOption.Set) {
            if (option == ModifyOption.Add) {
                foreach (T item in target) {
                    current.Add(item);
                }
            } else if (option == ModifyOption.Remove) {
                foreach (T item in target) {
                    current.Remove(item);
                }
            } else {
                current = target;
            }
    
        }

        public static IList<T> Shuffle<T>(this IList<T> list) {
            for (int i = 0; i < list.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, list.Count);

                T temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
            return list;
        }
    }

}
