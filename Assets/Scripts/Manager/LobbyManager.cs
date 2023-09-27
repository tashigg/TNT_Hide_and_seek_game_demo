

using System;
using System.Collections.Generic;
using Tashi.NetworkTransport;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyManager : Singleton<LobbyManager>
{
    [SerializeField] private bool isUsingTashi;
    private TashiNetworkTransport NetworkTransport => NetworkManager.Singleton.NetworkConfig.NetworkTransport as TashiNetworkTransport;
    private Lobby _lobby;
    public Lobby CurrentLobby
    {
        get {return _lobby; }
        set { _lobby = value;
            SerializeFieldLobby = new LobbyInstance(_lobby);
        }
    }
    public LobbyInstance SerializeFieldLobby;
    public bool isLobbyHost = false;
    public float nextHeartbeat; /* Time send heart beat to keep connection to lobby alive */
    public float nextLobbyRefresh; /* Time get update lobby info */
    /* If Tashi has already been set as a PlayerDataObject, we can set our own PlayerDataPbject in the Lobby. */
    public bool isSetInitPlayerDataObject = false;

    private void Update()
    {
        CheckLobbyUpdate();
    }
    public async void CheckLobbyUpdate()
    {
        if (CurrentLobby is null) return;
        if (Time.realtimeSinceStartup >= nextHeartbeat && isLobbyHost)
        {
            nextHeartbeat = Time.realtimeSinceStartup + 15;
            /* Keep connection to lobby alive */
            await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
        }

        if (Time.realtimeSinceStartup >= nextLobbyRefresh)
        {
            this.nextLobbyRefresh = Time.realtimeSinceStartup + 2; /* Update after every 2 seconds */
            this.LobbyUpdate();
            this.ReceiveIncomingDetail();
        }
    }
    
    /* Tashi setup/update PlayerDataObject */
    public async void LobbyUpdate()
    {
        var outgoingSessionDetails = NetworkTransport.OutgoingSessionDetails;

        var updatePlayerOptions = new UpdatePlayerOptions();
        if (outgoingSessionDetails.AddTo(updatePlayerOptions))
        {
            // Debug.Log("= PlayerData outgoingSessionDetails AddTo TRUE so can UpdatePLayerAsync");
            CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id,
                AuthenticationService.Instance.PlayerId,
                updatePlayerOptions);

           
        }
        
        if (isSetInitPlayerDataObject == false)
        {
            isSetInitPlayerDataObject = true;
            UpdatePlayerDataInCurrentLobby(CurrentLobby, AuthenticationService.Instance.Profile,
                isLobbyHost ? PlayerTypeInGame.Police.ToString() : PlayerTypeInGame.Thief.ToString(), false);
        }
        if (isLobbyHost)
        {
            var updateLobbyOptions = new UpdateLobbyOptions();
            if (outgoingSessionDetails.AddTo(updateLobbyOptions))
            {
                CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, updateLobbyOptions);
            }
        }
    }
    
    /* Tashi Update/get lobby session details */
    public async void ReceiveIncomingDetail()
    {
        try
        {
            if (NetworkTransport.SessionHasStarted) return;
            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            var incomingSessionDetails = IncomingSessionDetails.FromUnityLobby(CurrentLobby);

            // This should be replaced with whatever logic you use to determine when a lobby is locked in.
            if (incomingSessionDetails.AddressBook.Count >= 2)
            {
                NetworkTransport.UpdateSessionDetails(incomingSessionDetails);
            }
        }
        catch (Exception)
        {
        }
    }
    /* To update some Player Info through lobby such as name, isReady state, role */
    public async void UpdatePlayerDataInCurrentLobby(Lobby lobby, string name, string role, bool isReady)
    {
        /* Add Player Data into Lobby */
        try
        {
            //Ensure you sign-in before calling Authentication Instance
            //See IAuthenticationService interface
            string playerId = AuthenticationService.Instance.PlayerId;

            /* Find PlayerData for current this Player  */
            Player p = lobby.Players.Find(x => x.Id == playerId);
            if (p is null) return;
            Debug.Log("= UpdatePlayerDataInCurrentLobby : ID : " + p.Id);
            Dictionary<string, PlayerDataObject> oldData = p.Data;
            if (oldData is null)
            {
                oldData = new Dictionary<string, PlayerDataObject>();
            }

            UpdatePlayerOptions options = new UpdatePlayerOptions();
            // options.Data = oldData; 
            options.Data = new Dictionary<string, PlayerDataObject>();

            options.Data["Name"] = new PlayerDataObject(
                visibility: PlayerDataObject.VisibilityOptions.Public,
                value: name);
            options.Data["Role"] = new PlayerDataObject(
                visibility: PlayerDataObject.VisibilityOptions.Public,
                value: role);
            options.Data["IsReady"] = new PlayerDataObject(
                visibility: PlayerDataObject.VisibilityOptions.Public,
                value: isReady.ToString());

            CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id, playerId, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
    public async void UpdatePlayerDataIsReadyInLobby(bool isReady)
    {
        try
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions();

            options.Data = new Dictionary<string, PlayerDataObject>()
            {
                {
                    "IsReady", new PlayerDataObject(
                        visibility: PlayerDataObject.VisibilityOptions.Public,
                        value: isReady.ToString())
                }
            };

            //Ensure you sign-in before calling Authentication Instance
            //See IAuthenticationService interface
            string playerId = AuthenticationService.Instance.PlayerId;

            CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id, playerId, options);
            Debug.Log("= UpdatePlayerDataIsReadyInLobby : isReady " + isReady.ToString());
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }
    public async void ExitCurrentLobby()
    {
        if (CurrentLobby == null) return;
        /* Remove this player out of this lobby */
        if (CurrentLobby.Players.Count > 1)
        {
            await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
        }
        else
        {
            await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
        }

        isLobbyHost = false;
        isSetInitPlayerDataObject = false;
        CurrentLobby = null;
        NetworkManager.Singleton.Shutdown();
        
        if(MenuSceneManager.Instance is not null)
            MenuSceneManager.Instance.UpdateStatusText();
    }
    public void OnApplicationQuit()
    {
        ExitCurrentLobby();
    }
}

[System.Serializable]
public class LobbyInstance //Just for debug
{
    public string HostId, Id, LobbyCode, Upid, EnvironmentId, Name;
    public int MaxPlayers, AvailableSlots;
    public bool IsPrivate, IsLocked;

    public LobbyInstance(Lobby lobby)
    {
        HostId = lobby is null ? "" : lobby.HostId;
        Id = lobby is null ? "" : lobby.Id;
        LobbyCode = lobby is null ? "" : lobby.LobbyCode;
        Upid = lobby is null ? "" : lobby.Upid;
        EnvironmentId = lobby is null ? "" : lobby.EnvironmentId;
        Name = lobby is null ? "" : lobby.Name;

        MaxPlayers = lobby is null ? 8 : lobby.MaxPlayers;
        AvailableSlots = lobby is null ? 8 : lobby.AvailableSlots;

        IsPrivate = lobby is null ? false : lobby.IsPrivate;
        IsLocked = lobby is null ? false : lobby.IsLocked;
    }
}