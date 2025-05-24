using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IMonoBehaviour : MonoBehaviour
{
    protected virtual void Awake() {

    }
    
    protected virtual void Start() {
        Init();
    }

    public virtual void Init() {

    }

    protected virtual IEnumerator WaitForSeconds(float seconds, Action callback = null) {

        yield return new WaitForSeconds(seconds);

        callback?.Invoke();
    }

    protected virtual IEnumerator WaitForCondition(Func<bool> predicate, Action callback = null) {

        yield return new WaitUntil(predicate);

        callback?.Invoke();
    }
}
