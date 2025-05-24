using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPageHandler
{
    public int Page { get; }
    public int LastPage { get; }

    public void SetPage(int newPage);
    public void PrevPage();
    public void NextPage();

}
