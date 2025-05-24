using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class IToggle : IMonoBehaviour
{
    private Toggle toggle;

    protected override void Awake() {
        base.Awake();
        toggle = gameObject.GetComponent<Toggle>();
    }

}
