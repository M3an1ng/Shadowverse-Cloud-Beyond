using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Random = UnityEngine.Random;
using System;

public class NetworkManager : PunSingleton<NetworkManager>
{
    private NetworkData networkData = null;
    public event Action<Photon.Realtime.Player> onOtherPlayerJoinedRoomEvent;
    public event Action<Photon.Realtime.Player> onOtherPlayerLeftRoomEvent;
    public event Action<Photon.Realtime.Player> onPlayerPropsUpdateEvent;
    public event Action<short, string> onCreateOrJoinFailedEvent;
    public event Action<DisconnectCause, string> onDisconnectEvent;

    protected override void OnApplicationQuit()
    {
        PhotonNetwork.Disconnect();
        base.OnApplicationQuit();
    }

    public void StartNetworkAction(NetworkData data) {
        networkData = data;
        switch (data.networkAction) {
            case NetworkAction.Create:
            case NetworkAction.Join:
                ConnectToNetwork();
                return;
            case NetworkAction.Leave:
                ProcessNetworkData();
                return;
            default:
                return;
        }
    }

    private void ConnectToNetwork() {
        PhotonNetwork.LocalPlayer.NickName = Player.Nickname;
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster() {
        ProcessNetworkData();
    }

    private void ProcessNetworkData() {
        switch (networkData.networkAction) {
            case NetworkAction.Create:
                RoomOptions roomOptions = new RoomOptions() {
                    MaxPlayers = 2,
                    CustomRoomProperties = networkData.roomProperty,
                };
                PhotonNetwork.CreateRoom(networkData.roomName, roomOptions);
                break;
            case NetworkAction.Join:
                PhotonNetwork.JoinRoom(networkData.roomName);
                break;
            case NetworkAction.Leave:
                if (PhotonNetwork.InRoom)
                    PhotonNetwork.LeaveRoom();
                else
                    SceneLoader.instance.ChangeScene(SceneId.Main);
                break;
            default:
                break;
        }
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
    {   
        onPlayerPropsUpdateEvent?.Invoke(targetPlayer);
    }

    public override void OnCreatedRoom() {
        SceneLoader.instance.ChangeScene(SceneId.Room);
    }

    public override void OnCreateRoomFailed(short returnCode, string message) {
        if (returnCode == ErrorCode.GameIdAlreadyExists) {
            networkData.roomName = Random.Range(10001, 100000).ToString();
            OnConnectedToMaster();
            return;
        }
        string failedMessage = "創建房間失敗\n" + "失敗原因：" + message;
        RequestManager.OnRequestFail(failedMessage);
        PhotonNetwork.Disconnect();

        onCreateOrJoinFailedEvent?.Invoke(returnCode, failedMessage);
    }

    public override void OnJoinedRoom() {
        SceneLoader.instance.ChangeScene(SceneId.Room);
    }

    public override void OnJoinRoomFailed(short returnCode, string message) {
        message = (int)returnCode switch {
            ErrorCode.GameDoesNotExist => "房間號碼不存在",
            ErrorCode.GameClosed => "該房間不公開",
            ErrorCode.GameFull => "房間已滿",
            _ => message,
        };
        string failedMessage = "加入房間失敗\n" + "失敗原因：" + message;
        RequestManager.OnRequestFail(failedMessage);
        PhotonNetwork.Disconnect();

        onCreateOrJoinFailedEvent?.Invoke(returnCode, failedMessage);
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        onOtherPlayerJoinedRoomEvent?.Invoke(newPlayer);
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        onOtherPlayerLeftRoomEvent?.Invoke(otherPlayer);
    }

    public override void OnLeftRoom() {
        PhotonNetwork.Disconnect();
        SceneLoader.instance.ChangeScene(SceneId.Main);
    }

    public override void OnDisconnected(DisconnectCause disconnectCause) {
        networkData = null;
        PhotonNetwork.LocalPlayer.CustomProperties.Clear();
        if ((disconnectCause == DisconnectCause.None) || (disconnectCause == DisconnectCause.DisconnectByClientLogic))
            return;

        string message = disconnectCause switch {
            DisconnectCause.ClientTimeout => "連線已逾時",
            DisconnectCause.ServerTimeout => "連線已逾時",
            _ => disconnectCause.ToString(),
        };

        string failedMessage = "連線錯誤。\n" + message;
        RequestManager.OnRequestFail(failedMessage);
        onDisconnectEvent?.Invoke(disconnectCause, failedMessage);
    }
}

public class NetworkData {
    public NetworkAction networkAction = NetworkAction.Null;
    public string roomName = "99999";
    public Hashtable roomProperty = new Hashtable();
}

public enum NetworkAction {
    Null = 0,
    Create = 1,
    Join = 2,
    Leave = 3,
}
