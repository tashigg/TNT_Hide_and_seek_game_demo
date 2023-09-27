using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Cinemachine;
using StarterAssets;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Collections;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class PlayManager : NetworkBehaviour
{
    public static PlayManager Instance { get; private set; }
    [SerializeField] private CinemachineVirtualCamera _playerFollowCamera;
    [SerializeField] public UICanvasControllerInput uiCanvasControllerInput;
    [SerializeField] public TextMeshProUGUI _playerStatus;
    [SerializeField] public TextMeshProUGUI _amountPlayerOnline;
    [SerializeField] public TextMeshProUGUI _goalPointText;
        
    [Header("Popup End Game")]
    [SerializeField] private GameObject _popupEndGame;
    [SerializeField] private Transform _listPlayerEndGameTransform;
    [SerializeField] public bool _isEndGame = false;
    [SerializeField] public int _pointEndGame;
    [SerializeField] private TextMeshProUGUI[] listEndGamePlayerNameText;
    
    [Header("Logic Game")] 
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private Transform[] listSpawnBonusPosition; /* List positions could be choose for spawn new Bonus */
    [SerializeField] private GameObject policeBonusPrefab;
    [SerializeField] private GameObject thiefBonusPrefab;
    [SerializeField] private int maxPoliceBonus;  /* Max Police Bonus could be spawn in game */
    [SerializeField] private int maxThiefBonus; /* Max Thief Bonus could be spawn in game */
    private List<ulong> listPoliceBonusIdSpawned = new List<ulong>(); /* Store List Police Bonus are spawned in game */
    private List<ulong> listThiefBonusIdSpawned = new List<ulong>(); /* Store List Thief Bonus are spawned in game */
    [SerializeField] public Transform policeSpawnTransform;
    [SerializeField] public Transform thiefSpawnTransform;
    [SerializeField] public GameObject explosionBoomPrefab;

    private NetworkVariable<int> playersInRoom = new NetworkVariable<int>();
    [SerializeField] private TextMeshProUGUI[] listPlayerNameText;
    public int PlayersInRoom
    {
        get { return playersInRoom.Value; }
    }
    Dictionary<ulong, NetCodeThirdPersonController> playersList = new Dictionary<ulong, NetCodeThirdPersonController>();
    public Dictionary<ulong, NetCodeThirdPersonController> PlayersList { get => playersList; }
    public List<ulong> m_connectedClients = new List<ulong>();
    public CinemachineVirtualCamera PlayerFollowCamera
    {
        get
        {
            if (_playerFollowCamera == null)
            {
                _playerFollowCamera = FindObjectOfType<CinemachineVirtualCamera>();
            }
            return _playerFollowCamera;
        }
    }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        _goalPointText.text = $"Goal point to win game : {_pointEndGame}";
        _popupEndGame.SetActive(false);
        _isEndGame = false;
        foreach (TextMeshProUGUI item in listPlayerNameText)
        {
            item.gameObject.SetActive(false);
        }
        _playerStatus.text = PlayerDataManager.Instance.playerData.status.ToString();

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log($"= Start PlaySceneManager : ClientID : {OwnerClientId} IsServer : " + IsServer.ToString());
        if (IsServer)
        {
            SpawnBonusPrefabServerRpc();
        }
    }

    public void ServerSceneInit(ulong clientId)
    {
        Debug.Log("= PlayManager ServerSceneInit : " + clientId);
        
        // Save the clients 
        m_connectedClients.Add(clientId);
        Debug.Log("= ConnectedClients.Count : " + NetworkManager.Singleton.ConnectedClients.Count);
        // Check if is the last client
        if (m_connectedClients.Count < NetworkManager.Singleton.ConnectedClients.Count)
            return;
        // For each client spawn and set UI
        foreach (var client in m_connectedClients)
        {

            GameObject player = NetworkObjectSpawner.SpawnNewNetworkObjectAsPlayerObject(
                _playerPrefab, policeSpawnTransform.position, client, true
                );

            NetCodeThirdPersonController playerNetCodeThirdPersonController =
                player.GetComponent<NetCodeThirdPersonController>();
            PlayersList.Add(client, playerNetCodeThirdPersonController);
            
        }
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"= PlaySceneManager ClientID : {OwnerClientId} OnNetworkSpawn");
        if (IsServer)
        {
            playersInRoom.Value++;
        }
    }
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client ID {clientId} just Connected...");
        if (IsServer)
        {
            /* If you put this code outside IsServer : get error cannot write */
            playersInRoom.Value++;
        }
    }
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client ID {clientId} just disconnected...");
        if (IsServer)
        {
            playersInRoom.Value--;
        }
    }

    void Update()
    {
        if (_isEndGame) return;
        _amountPlayerOnline.text = PlayersInRoom.ToString();
        int i = 0;
        foreach (KeyValuePair<ulong, NetCodeThirdPersonController> player in PlayersList)
        {
            // Debug.LogWarning($"= Client ID {player.Key} has Name {player.Value.PlayerName}");
            if (i <= listPlayerNameText.Length)
            {
                listPlayerNameText[i].text = string.Format("#{0}: {3} - {1} - ID : {2} - P : {4}", i + 1, player.Value.PlayerName, player.Key.ToString(), player.Value.TypeInGame.ToString(), player.Value.Point);
                listPlayerNameText[i].gameObject.SetActive(true);
                i++;
            }
        }
        for (int j = i; j <= listPlayerNameText.Length - 1; j++)
        {
            listPlayerNameText[j].gameObject.SetActive(false);
        }
    }
    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();
    }
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
    }
    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }
    public void DisconnectClient()
    {
        Debug.Log("== Disconnect this client ID : " + NetworkManager.Singleton.LocalClientId);
        NetworkManager.Singleton.Shutdown();
        
        // Cleanup();
        PlayerDataManager.Instance.SetStatus(PlayerStatus.Offline);
        LobbyManager.Instance.ExitCurrentLobby();
        StartCoroutine(IELoadMenuScene());
        
    }

    public IEnumerator IELoadMenuScene()
    {
        yield return new WaitForSeconds(2f);
        // NetworkManager.SceneManager.LoadScene(SceneName.Menu.ToString());
        SceneManager.LoadScene(SceneName.Menu.ToString());
    }
    public void Cleanup()
    {
        if (NetworkManager.Singleton != null)
        {
            Destroy(NetworkManager.Singleton.gameObject);
        }
    }


    #region ServerRPC 
    [ServerRpc(RequireOwnership = false)]
    private void SpawnBonusPrefabServerRpc()
    {
        Debug.Log("= SpawnBonusPrefabServerRpc");

        while (listPoliceBonusIdSpawned.Count < maxPoliceBonus)
        {
            /* Spawn Police Bonus Prefab */
            GameObject bonusP = Instantiate(policeBonusPrefab, listSpawnBonusPosition[Random.Range(0, listSpawnBonusPosition.Length)].position, Quaternion.identity);
            bonusP.transform.position += new Vector3(Random.Range(-1f, 1f), 1f, Random.Range(-1f, 1f));
            NetworkObject bonusPoliceNetworkObj = bonusP.GetComponent<NetworkObject>();
            bonusPoliceNetworkObj.Spawn();
            listPoliceBonusIdSpawned.Add(bonusPoliceNetworkObj.NetworkObjectId);
        }

        while (listThiefBonusIdSpawned.Count < maxPoliceBonus)
        {
            /* Spawn Police Bonus Prefab */
            GameObject bonusT = Instantiate(thiefBonusPrefab, listSpawnBonusPosition[Random.Range(0, listSpawnBonusPosition.Length)].position, Quaternion.identity);
            bonusT.transform.position += new Vector3(Random.Range(-1f, 1f), 1f, Random.Range(-1f, 1f));
            NetworkObject bonusThiefNetworkObj = bonusT.GetComponent<NetworkObject>();
            bonusThiefNetworkObj.Spawn();
            listThiefBonusIdSpawned.Add(bonusThiefNetworkObj.NetworkObjectId);
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void PoliceTouchedPoliceBonusServerRpc(ulong bonusId, ServerRpcParams serverRpcParams = default)
    {
        var senderId = serverRpcParams.Receive.SenderClientId;
        Debug.Log($"= PoliceTouchedPoliceBonusServerRpc: SenderID {senderId} touched BonusId : {bonusId}");
        /* Player touched Bonus Item : Add Bonus Value, Despawn and Spawn new bonus Item in other place after a time  */
        /* Get bonus object spawned by id */
        NetworkObject bonusItem = NetworkManager.Singleton.SpawnManager.SpawnedObjects[bonusId];
        listPoliceBonusIdSpawned.Remove(bonusId); /* Remove from list */
        bonusItem.Despawn(); /* Despawn on Server : apply for all clients */
        SpawnBonusPrefabServerRpc(); /* Re-calc and spawn bonus item if posible */

        /* Increase Point for Police */
        NetCodeThirdPersonController sender = PlayersList[senderId];
        if(sender != null){
            sender.point.Value += bonusItem.GetComponent<BonusItem>().bonusData.value;
        }
        

    }
    [ServerRpc(RequireOwnership = false)]
    public void ThiefTouchedThiefBonusServerRpc(ulong bonusId, ServerRpcParams serverRpcParams = default)
    {
        var senderId = serverRpcParams.Receive.SenderClientId;
        Debug.Log($"= ThiefTouchedThiefBonusServerRpc: SenderID {senderId} touched BonusId : {bonusId}");
        /* Thief touched Bonus Item : Add More points, Despawn and Spawn new bonus Item in other place after a time  */
        /* Get bonus object spawned by id */
        NetworkObject bonusItem = NetworkManager.Singleton.SpawnManager.SpawnedObjects[bonusId];
        listThiefBonusIdSpawned.Remove(bonusId); /* Remove from list */
        bonusItem.Despawn(); /* Despawn on Server : apply for all clients */
        SpawnBonusPrefabServerRpc(); /* Re-calc and spawn bonus item if posible */

        /* Increase Point for Thief */
        NetCodeThirdPersonController sender = PlayersList[senderId];
        if(sender != null){
            sender.point.Value += bonusItem.GetComponent<BonusItem>().bonusData.value;
        }
    }
    [ServerRpc(RequireOwnership = false)]
    public void ShowPopupEndGameServerRpc(ServerRpcParams serverRpcParams = default)
    {
        Debug.Log("== ShowPopupEndGameServerRpc trigger. Broadcast event end game to all clients");
        ShowPopupEndGameClientRpc();
    }
    #endregion

    #region ClientRpc

    [ClientRpc]
    public void ShowPopupEndGameClientRpc()
    {
        _isEndGame = true;
        _popupEndGame.SetActive(true);
        /* Set active false for all item list player */
        foreach (Transform child in _listPlayerEndGameTransform)
        {
            child.gameObject.SetActive(false);
        }
        /* Re-fill list player data and score to table list player */
        int i = 0;
        foreach (KeyValuePair<ulong, NetCodeThirdPersonController> player in PlayersList)
        {
            // Debug.LogWarning($"= Client ID {player.Key} has Name {player.Value.PlayerName}");
            if (i <= listEndGamePlayerNameText.Length)
            {
                listEndGamePlayerNameText[i].text = string.Format("#{0}: {3} - {1} - ID : {2} - P : {4}", i + 1, player.Value.PlayerName, player.Key.ToString(), player.Value.TypeInGame.ToString(), player.Value.Point);
                listEndGamePlayerNameText[i].gameObject.SetActive(true);
                if (player.Value.Point >= _pointEndGame)
                {
                    listEndGamePlayerNameText[i].fontSize = listEndGamePlayerNameText[i].fontSize + 15;
                    listEndGamePlayerNameText[i].fontStyle = FontStyles.Bold;
                }
                i++;
            }
        }
    }

    #endregion
}
