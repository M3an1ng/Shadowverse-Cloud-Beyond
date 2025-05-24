using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PortalSearchModel : IMonoBehaviour
{
    //改为使用字典存储
    public CardFilter filter = new CardFilter(1);

    public void Clear() {
        filter = new CardFilter(1);
    }

    public void SetBool(string which, bool item) {
        filter.SetBool(which, item);
    }

    public void SetInt(string which, int item) {
        filter.SetInt(which, item);
    }

    public void SetString(string which, string input) {
        filter.SetString(which, input);
    }

    public void SelectInt(string which, int item) {
        filter.SelectInt(which, item);
    }

}
