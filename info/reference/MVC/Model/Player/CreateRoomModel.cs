using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomModel :IMonoBehaviour
{
    [SerializeField] private IInputField inputField;
    [SerializeField] private Dropdown zoneDropdown, formatDropdown, bestOfDropdown;
    public string roomNum => inputField.InputString;
    public int zone => zoneDropdown.value + 1;
    public int format => formatDropdown.value;
    public int bestOf => bestOfDropdown.value + 1;

    public void CreateRoom() {
        OnCreateOrJoinRoom();
        NetworkData networkData = new NetworkData() {
            networkAction = NetworkAction.Create,
            roomName = Random.Range(10001, 100000).ToString(),
        };
        networkData.roomProperty["zfb"] = zone * 100 + format * 10 + bestOf;
        NetworkManager.instance.StartNetworkAction(networkData);
    }

    public void JoinRoom() {
        OnCreateOrJoinRoom();
        NetworkData networkData = new NetworkData() {
            networkAction = NetworkAction.Join,
            roomName = roomNum,
        };
        NetworkManager.instance.StartNetworkAction(networkData);
    }

    private void OnCreateOrJoinRoom() {
        Player.currentDeck = new Deck((CardZone)zone, (GameFormat)format, CardCraft.Neutral);
        var hash = PhotonNetwork.LocalPlayer.CustomProperties;
        hash["win"] = 0;
        PhotonNetwork.LocalPlayer.SetCustomProperties(hash);
    }
}
