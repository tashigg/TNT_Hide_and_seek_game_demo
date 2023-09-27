using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using UnityEngine.SceneManagement;
using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Random = UnityEngine.Random;
using Tashi.NetworkTransport;
using Unity.Collections;

public class MenuSceneManager : NetworkBehaviour
{
    public static MenuSceneManager Instance { get; private set; }
    [SerializeField] private GameObject profileMenu;
    [SerializeField] private GameObject lobbyMenu;

    [Header("Profile Menu")] [SerializeField]
    private TMP_InputField _nameTextField;

    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button signInButton;

    public TashiNetworkTransport NetworkTransport =>
        NetworkManager.Singleton.NetworkConfig.NetworkTransport as TashiNetworkTransport;

    [Header("Lobby Menu")] [SerializeField]
    private TMP_InputField _numberPlayerInRoomTextField;

    [SerializeField] private TMP_InputField _roomCodeToJoinTextField;
    [SerializeField] private TMP_InputField _roomCodeLobbyTextField; /* room code of lobby you are in */


    [SerializeField]
    private GameObject _lobbyFreeGroup; /* Include buttons, components when are free, not in any lobby or room */

    [SerializeField] private GameObject _inLobbyGroup; /* Include buttons, components when are in lobby */
    [SerializeField] private Button _createLobbyButton;
    [SerializeField] private Button _joinLobbyButton;
    [SerializeField] private Button _exitLobbyButton;
    [SerializeField] private Button _startRoomButton;
    [SerializeField] private Button _readyRoomButton;

    [Header("List PLayers in Room")] [SerializeField]
    private Transform _listPlayersContentTransform;

    [SerializeField] private PlayerItem _playerItemPrefab;
    public List<PlayerItem> listPlayers = new();

    [Header("List Lobbies")] [SerializeField]
    private Button _reloadListLobbiesButton;

    /* Transform of Gameobject that'll contain list lobby item */
    [SerializeField] private Transform _listLobbiesContentTransform;

    /* Lobby Item prefab */
    [SerializeField] private LobbyItem _lobbyItemPrefab;

    /* List store all lobby item created */
    public List<LobbyItem> listLobbies = new();

    private int _playerCount = 0; /* Number player in lobby */
    private int _clientCount = 0; /* total clients are connect */

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        // UnityServicesInit();
    }

    public IEnumerator Start()
    {
        string name = PlayerPrefs.GetString(Constants.NAME_PREF, "");
        PlayerDataManager.Instance.SetName(name);
        _nameTextField.text = name;
        /* Listen player name text field value changed */
        _nameTextField.onValueChanged.AddListener(delegate { OnPlayerNameChange(); });

        _createLobbyButton.onClick.AddListener(CreateLobby);
        _joinLobbyButton.onClick.AddListener(JoinLobbyButtonClick);

        _reloadListLobbiesButton.onClick.AddListener(ListLobbies);

        _startRoomButton.onClick.AddListener(StartRoom);
        _readyRoomButton.onClick.AddListener(ToggleReadyState);
        _exitLobbyButton.onClick.AddListener(LobbyManager.Instance.ExitCurrentLobby);

        CheckAuthentication();

        // Wait for the network Scene Manager to start
        yield return new WaitUntil(() => NetworkManager.Singleton.SceneManager != null);
        // Set the events on the loading manager
        // Doing this because every time the network session ends the loading manager stops
        // detecting the events
        LoadingSceneManager.Instance.Init();

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client ID {clientId} just Connected...");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client ID {clientId} just disconnected...");
    }

    public async void ToggleReadyState()
    {
        Lobby lobby = await LobbyService.Instance.GetLobbyAsync(LobbyManager.Instance.CurrentLobby.Id);
        Player p = lobby.Players.Find(x => x.Id == AuthenticationService.Instance.PlayerId);
        if (p is null) return;
        string _isReadyStr = p.Data["IsReady"].Value;
        bool _isReady = bool.Parse(_isReadyStr);
        LobbyManager.Instance.UpdatePlayerDataIsReadyInLobby(!_isReady);
    }

    void Update()
    {
        CheckLobbyUpdate();
    }

    public IEnumerator IECheckUpdateListPLayerInRoom()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (AuthenticationService.Instance.IsSignedIn && LobbyManager.Instance.CurrentLobby is not null &&
                !string.IsNullOrEmpty(LobbyManager.Instance.CurrentLobby.Id))
            {
                // Debug.Log("= IECheckUpdateListPLayerInRoom");
                /* Disative all old lobby item in list */
                foreach (Transform child in _listPlayersContentTransform)
                {
                    child.gameObject.SetActive(false);
                }

                listPlayers.Clear();
                /* Show every lobby item in list */
                int i = 0;

                foreach (Player p in LobbyManager.Instance.CurrentLobby.Players)
                {
                    try
                    {
                        // Debug.Log("====== PLAYER DATA OBJECT AFTER =====");
                        // foreach (KeyValuePair<string, PlayerDataObject> k in p.Data)
                        //     Debug.Log($"= Key : {k.Key.ToString()} and Value = {k.Value.Value.ToString()}");

                        if (p.Data["Name"] != null && p.Data["Role"] != null && p.Data["IsReady"] != null)
                        {
                            i++;
                            PlayerItem playerItem;
                            try
                            {
                                playerItem = _listPlayersContentTransform.GetChild(i).GetComponent<PlayerItem>();
                            }
                            catch (Exception)
                            {
                                playerItem = Instantiate(_playerItemPrefab, _listPlayersContentTransform);
                            }

                            playerItem.SetData("#" + (i), p.Data["Name"].Value, p.Data["Role"].Value.ToString(),
                                bool.Parse(p.Data["IsReady"].Value));
                            listPlayers.Add(playerItem);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        // Debug.Log("== Error when show list player : " + e.ToString());
                        continue;
                    }
                }
            }
        }
    }

    void CheckAuthentication()
    {
        /* Check signed in */
        if (AuthenticationService.Instance.IsSignedIn)
        {
            profileMenu.SetActive(false);
            lobbyMenu.SetActive(true);
        }
        else
        {
            profileMenu.SetActive(true);
            lobbyMenu.SetActive(false);
        }

        UpdateStatusText();
    }

    public void OnPlayerNameChange()
    {
        Debug.Log("OnPlayerNameChange : " + _nameTextField.text);
        PlayerDataManager.Instance.SetName(_nameTextField.text);
    }

    public async void SignInButtonClicked()
    {
        if (string.IsNullOrEmpty(_nameTextField.text))
        {
            Debug.Log($"Signing in with the default profile");
            // await UnityServices.InitializeAsync();
        }
        else
        {
            Debug.Log($"Signing in with profile '{_nameTextField.text}'");
            /* Init Unity Services. But now no need cause inited in Awake() */
            // var options = new InitializationOptions();
            // options.SetProfile(_nameTextField.text);
            // await UnityServices.InitializeAsync(options);

            /* Switch to new Profile name. Profile init in awake() is default */
            AuthenticationService.Instance.SwitchProfile(_nameTextField.text);
        }

        try
        {
            signInButton.interactable = false;
            statusText.text = $"Signing in .... ";
            AuthenticationService.Instance.SignedIn += delegate
            {
                Debug.Log("SignedIn OK!");
                signInButton.interactable = true;
                PlayerDataManager.Instance.SetId(AuthenticationService.Instance.PlayerId);
                UpdateStatusText();
                profileMenu.SetActive(false);
                lobbyMenu.SetActive(true);

                StopCoroutine(IEGetListLobbies());
                StartCoroutine(IEGetListLobbies());
                
                StopCoroutine(IECheckUpdateListPLayerInRoom());
                StartCoroutine(IECheckUpdateListPLayerInRoom());
            };

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            signInButton.interactable = true;
            statusText.text = $"Sign in failed : {e.ToString()} ";
            Debug.LogException(e);
            throw;
        }
    }

    IEnumerator IEGetListLobbies(float delayTime = 3f)
    {
        while (true)
        {
            if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
            {
                ListLobbies();
            }

            yield return new WaitForSeconds(delayTime);
        }
    }

    public void UpdateStatusText()
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            statusText.text =
                $"Signed in as {AuthenticationService.Instance.Profile} (ID:{AuthenticationService.Instance.PlayerId}) in Lobby";
            // Shows how to get an access token
            statusText.text += $"\n{_clientCount} peer connections";
        }
        else
        {
            statusText.text = "Not Sign in yet";
        }

        if (LobbyManager.Instance.CurrentLobby is null)
        {
        }
        else
        {
            statusText.text +=
                $"\n In Lobby ID : {LobbyManager.Instance.CurrentLobby.Id} has code : {LobbyManager.Instance.CurrentLobby.LobbyCode}";
            statusText.text += $"\n {_playerCount} players in lobby.";
        }
    }

    public async void JoinLobbyButtonClick()
    {
        JoinLobbyByLobbyCode(_roomCodeToJoinTextField.text);
    }

    public async void JoinLobbyByLobbyId(string lobbyId)
    {
        /* Start Client when Join Lobby as client */
        // NetworkManager.Singleton.StartClient();
        LobbyManager.Instance.CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
        SetupDataAfterJoinLobby();
    }

    public async void JoinLobbyByLobbyCode(string lobbyCode)
    {
        /* Start Client when Join Lobby as client */
        // NetworkManager.Singleton.StartClient();
        LobbyManager.Instance.CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

        SetupDataAfterJoinLobby();
    }

    public void SetupDataAfterJoinLobby()
    {
        Debug.Log(
            $"Join lobby Id {LobbyManager.Instance.CurrentLobby.Id} has code {LobbyManager.Instance.CurrentLobby.LobbyCode}");
        LobbyManager.Instance.isLobbyHost = false;
        _roomCodeLobbyTextField.text = LobbyManager.Instance.CurrentLobby.LobbyCode;
        UpdateStatusText();
        LobbyManager.Instance.isSetInitPlayerDataObject = false;
    }

    public async void CreateLobby()
    {
        int maxPlayerInRoom = 8;
        if (int.TryParse(_numberPlayerInRoomTextField.text, out int rs))
        {
            maxPlayerInRoom = rs;
        }
        else
        {
            maxPlayerInRoom = 8;
        }

        _numberPlayerInRoomTextField.text = maxPlayerInRoom.ToString();

        // NetworkManager.Singleton.StartHost();

        var lobbyOptions = new CreateLobbyOptions
        {
            IsPrivate = false,
        };
        string lobbyName = this.LobbyName();

        LobbyManager.Instance.CurrentLobby =
            await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayerInRoom, lobbyOptions);
        LobbyManager.Instance.isLobbyHost = true;
        _roomCodeLobbyTextField.text = LobbyManager.Instance.CurrentLobby.LobbyCode;
        Debug.Log(
            $"= Create Lobby name : {lobbyName} has max {maxPlayerInRoom} players. Lobby Code {LobbyManager.Instance.CurrentLobby.LobbyCode}");
        UpdateStatusText();
        LobbyManager.Instance.isSetInitPlayerDataObject = false;
    }

    public async void CheckLobbyUpdate()
    {
        /* If Free, not in any lobby, show suiable UI */
        if (LobbyManager.Instance.CurrentLobby is null)
        {
            this._lobbyFreeGroup.SetActive(true);
            this._inLobbyGroup.SetActive(false);
            return;
        }

        /* If Are in lobby, just show suiable UI */
        this._lobbyFreeGroup.SetActive(false);
        this._inLobbyGroup.SetActive(true);

        _startRoomButton.gameObject.SetActive(LobbyManager.Instance.isLobbyHost);
        _readyRoomButton.gameObject.SetActive(!LobbyManager.Instance.isLobbyHost);


        this._playerCount = LobbyManager.Instance.CurrentLobby.Players.Count;

        /* Check if CurrentLobby has IsLocked = true, so it's ready to start game  */
        if (LobbyManager.Instance.CurrentLobby.IsLocked)
        {
            StartClient();
        }

        /* Update some status text in UI :  */
        UpdateStatusText();
    }

    public async void ListLobbies()
    {
        try
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    /* Just get the lobby's available slots using the filter. */
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            /* Disative all old lobby item in list */
            foreach (Transform child in _listLobbiesContentTransform)
            {
                child.gameObject.SetActive(false);
            }

            listLobbies.Clear();
            /* Show every lobby item in list */
            int i = 0;
            foreach (Lobby lobby in queryResponse.Results)
            {
                LobbyItem lobbyItem;
                try
                {
                    lobbyItem = _listLobbiesContentTransform.GetChild(i).GetComponent<LobbyItem>();
                }
                catch (Exception)
                {
                    lobbyItem = Instantiate(_lobbyItemPrefab, _listLobbiesContentTransform);
                }

                lobbyItem.SetData("#" + (i + 1), lobby.Id, lobby.LobbyCode, lobby.Name);
                lobbyItem.SetOnClickJoin(OnClickJoinLobby);
                listLobbies.Add(lobbyItem);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Exception : " + e.ToString());
        }
    }

    public void OnClickJoinLobby(string lobbyId)
    {
        if (LobbyManager.Instance.CurrentLobby is null)
            JoinLobbyByLobbyId(lobbyId);
    }

    public string LobbyName()
    {
        return AuthenticationService.Instance.Profile + "_lobby_" + Random.Range(1, 100);
    }

    public void StartRoom()
    {
        Debug.Log("= Start Room Clicked");
        PlayerDataManager.Instance.SetName(AuthenticationService.Instance.Profile);
        PushEventStartRoomViaLobbyData();
        AsyncOperation progress = SceneManager.LoadSceneAsync(SceneName.Play.ToString(), LoadSceneMode.Single);

        progress.completed += (op) => { NetworkManager.Singleton.StartHost(); };
    }

    public async void PushEventStartRoomViaLobbyData()
    {
        try
        {
            Debug.Log("PushEventStartRoomViaLobbyData : isLocked Before : " +
                      LobbyManager.Instance.CurrentLobby.IsLocked);

            UpdateLobbyOptions options = new UpdateLobbyOptions();
            options.IsLocked = true;

            LobbyManager.Instance.CurrentLobby =
                await LobbyService.Instance.UpdateLobbyAsync(LobbyManager.Instance.CurrentLobby.Id, options);

            Debug.Log(
                "PushEventStartRoomViaLobbyData : isLocked After : " + LobbyManager.Instance.CurrentLobby.IsLocked);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public void StartHost()
    {
        Debug.Log("= Start Host Clicked");
        PlayerDataManager.Instance.SetName(AuthenticationService.Instance.Profile);

        AsyncOperation progress = SceneManager.LoadSceneAsync(SceneName.Play.ToString(), LoadSceneMode.Single);

        progress.completed += (op) => { NetworkManager.Singleton.StartHost(); };
    }

    public void StartClient()
    {
        PlayerDataManager.Instance.SetName(AuthenticationService.Instance.Profile);
        AsyncOperation progress = SceneManager.LoadSceneAsync(SceneName.Play.ToString(), LoadSceneMode.Single);
        progress.completed += (op) =>
        {
            PlayerDataManager.Instance.SetStatus(PlayerStatus.InRoom);
            NetworkManager.Singleton.StartClient();
            Debug.Log("Started Client");
        };
    }

    public void OnApplicationQuit()
    {
    }

    #region ServerRpc

    [ServerRpc(RequireOwnership = false)]
    private void LoadSceneServerRpc(FixedString64Bytes name)
    {
        Debug.Log("= ServerRpc : LoadSceneServerRpc");
        SceneAboutToChangeClientRpc();
        foreach (var client in NetworkManager.Singleton.ConnectedClients.Values)
        {
            if (client.PlayerObject != null)
                client.PlayerObject.Despawn();
        }

        NetworkManager.SceneManager.LoadScene(SceneName.Play.ToString(), LoadSceneMode.Single);
    }

    #endregion

    #region ClientRpc

    [ClientRpc]
    private void SceneAboutToChangeClientRpc()
    {
        Debug.Log("= ClientRpc : SceneAboutToChangeClientRpc");
        // sceneIsLoading = true;
    }

    #endregion
}