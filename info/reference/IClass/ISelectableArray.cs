using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ISelectableArray<T>
{
    /// <summary>
    /// Number of items player should select. 0 means unlimited.
    /// </summary>
    public int SelectCount { get; protected set; } = 1;
    public int Capacity { get; protected set; } = 6;
    public int Size => Items.Count(x => x != null);
    public int Length => Items.Length;
    public bool[] IsSelected { get; protected set; }
    public int[] CurrentSelectIndex => IsSelected?.AllIndexOf(true);
    public T[] CurrentSelectItems => Items.Where((x, i) => IsSelected[i]).ToArray();

    /// <summary>
    /// Number of items player currently selects.
    /// </summary>
    public int CurrentSelectCount => IsSelected.Count(x => x);
    public bool[] Locks { get; protected set; }
    public T[] Items { get; protected set; }
    
    public T this[int n] => n.IsInRange(0, Items.Length) ? Items[n] : default(T);

    private Action<int> OnSelectCallback;

    public ISelectableArray() { 
        SetSelectCount(1);
        SetCapacity(6); 
    }

    public ISelectableArray(int capacity) {
        SetSelectCount(1);
        SetCapacity(capacity);
    }

    public ISelectableArray(int selectCount, int capacity) {
        SetSelectCount(selectCount);
        SetCapacity(capacity);
    }

    public void SetArray(T[] array, bool keepSelected = false) {
        Items = array;
        SetCapacity(Mathf.Max(array.Length, Capacity));
        InitLock();
        InitSelectedFlag(keepSelected);
    }

    private void InitSelectedFlag(bool keepSelected) {
        int[] oldSelected = CurrentSelectIndex;
        IsSelected = new bool[Capacity];
        if (keepSelected && (oldSelected != null)) {
            foreach (var index in oldSelected)
                Select(index);
        }
    }

    private void InitLock() {
        Locks = new bool[Capacity];
    }

    public void Lock(int[] _locks) {
        if (Locks == null)
            return;
        foreach (var id in _locks) {
            if (id <= Locks.Length)
                continue;
            Locks[id] = true;
        }
    }

    public void UnLock(int[] _keys) {
        if (Locks == null)
            return;
        foreach (var id in _keys) {
            if (id <= Locks.Length)
                continue;
            Locks[id] = false;
        }
    }

    public void SetSelectCount(int count) {
        int diff = SelectCount - count;
        if ((diff == 0) || (count < 0))
            return;

        if (diff > 0) {
            int s = CurrentSelectCount;
            for(int i = 0; i < s; i++) {
                Select(CurrentSelectIndex.Last());
            }
        }
        SelectCount = count;
    }

    public void SetCapacity(int c) {
        if (c <= 0)
            return;
        Capacity = c;
    }

    public void Select(int index) {
        if ((Items == null) || (Items.Length <= index))
            return;
        if (Locks[index])
            return;
        if (SelectCount == 1) {
            if (IsSelected[index])
                return;
            if (CurrentSelectCount > 0)
                IsSelected[CurrentSelectIndex[0]] = false;

            IsSelected[index] = true;
            OnSelectCallback?.Invoke(index);
            return;
        }
        if (IsSelected[index] || (SelectCount == 0) || (CurrentSelectCount < SelectCount)) {
            IsSelected[index] = !IsSelected[index];
            OnSelectCallback?.Invoke(index);
        }
    }

    public void SetOnSelectCallback(Action<int> callback) {
        OnSelectCallback = callback;
    }
    
}
