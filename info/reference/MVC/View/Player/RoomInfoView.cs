using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class RoomInfoView : IMonoBehaviour
{
    [SerializeField] private Text roomNumText, zoneText, formatText, bestOfText;

    public override void Init()
    {
        base.Init();
        InitRoomInfo();
    }

    private void InitRoomInfo() {
        var prop = PhotonNetwork.CurrentRoom.CustomProperties;
        int zfb = (int)prop["zfb"];
        int zone = zfb % 1000 / 100;
        int format = zfb % 100 / 10;
        int bestOf = zfb % 10;

        roomNumText?.SetText(PhotonNetwork.CurrentRoom.Name);

        zoneText?.SetText(((CardZone)zone).GetZoneName());
        formatText?.SetText(((GameFormat)format).GetFormatName());
        bestOfText?.SetText(((BestOf)bestOf).GetBestOfName());
    }

}
