using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptionSelectController : IMonoBehaviour
{
    [SerializeField] private bool defaultSelect = true;
    [SerializeField] private OptionSelectModel selectModel;
    [SerializeField] private OptionSelectView selectView;

    public override void Init()
    {
        base.Init();
        if (defaultSelect)
            Select(0);
    }


    public void Select(int index) {
        selectModel.Select(index);
        selectView.SetSelectState(selectModel.IsSelected);
    }

}
