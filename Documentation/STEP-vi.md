# Simple Tashi Step by step : 

## Import base package before Tashi : 
- In Unity Editor > Package manager > Unity Registry : Find and Install some packages (Netcode of gameobjects , Lobby, Relay maybe).

## Import newest Tashi package : 
- 28/7/2023 : 0.3.0 is available here : https://github.com/tashigg/tashi-network-transport/releases/tag/v0.3.0  (Update Tashi Relay)
- Download it and put tgz into folder Assets > Plugins 
- In Unity Editor > Window > Package Manager > Add package from tar ball > Choose Tashi.tgz from Assets > Plugins. 

- Tashi Relay URL https://eastus.relay.infra.tashi.dev/  ( Steven )

## Add Network Manager and Funtional button : 
- Create empty GameObject, add Network Manager component : Choose Transport protocol, drag player prefab and setup network prefabs lists.
- Choose Tashi Network Transport : Fill setup for relay base url or not. 
- Create in UI some buttons such as Start Server, Start Host and Start Client. Each button will call the function corresponding to function in Network Manager. 
- Create empty GameObject and attach new script to manage this scene. Script maybe have name like PlayManager.cs 
- Open PlayManager.cs and add some basically func : Start Host, Start Server, Start Client. using Unity.NetCode to use NetworkManager.Singleton or get info about NetCode.
- Add onClick for 3 button that we created before
- In Player Prefabs : Add component Network Object, Network Transform, Network Animation .. for sync some basic info. 
- Here I override Network Transform and Animation to Client Network Transform, Client Network Animation to turn off authoriatative from server. Trust on your clients.

* When import using Tashi, got an error when build to Mac app : The type 'Random' is defined in an assembly that is not referenced. You must add a reference to assembly 'Unity.Mathematics, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'.  So back to using NetCode for continues.

## Setup Player : Move, control 
* Target : Setup for control right client owned, camera follow client owned. 
- In Third Person Control : In Update > Check if(!IsOwner) return; so if you're not owner of this client, you can control. 
- In Player Prefabs : Untick Player Input. We'll detect and setup player input for localPlayer(or IsOwner Player) : Call this func when OnNetworkSpawn() : 
```c# 
    /* Setup for owner player : Camera, Player Input Movement, ... */
        protected void StartLocalPlayer()
        {
            if (IsClient && IsOwner)
            {
                _playerInput = GetComponent<PlayerInput>();
                _playerInput.enabled = true;
                PlayManager.Instance.PlayerFollowCamera.Follow = CinemachineCameraTarget.transform;
                _input = GetComponent<StarterAssetsInputs>();
                PlayManager.Instance.uiCanvasControllerInput.starterAssetsInputs = _input;
            }
        }
```
- In PlayManager.cs, create variable for PlayerFollowCamera, refer it from editor or load from script : 
```c# 
[SerializeField] private CinemachineVirtualCamera _playerFollowCamera;
    public CinemachineVirtualCamera PlayerFollowCamera { 
        get { 
            if(_playerFollowCamera == null){
                _playerFollowCamera = FindObjectOfType<CinemachineVirtualCamera>();
            }
            return _playerFollowCamera; 
        } 
    }
```
- At Start func in Player script, check FollowCamera if it's owner and local player. 
```c# 

if (IsLocalPlayer && IsOwner)
            {
                PlayManager.Instance.PlayerFollowCamera.Follow = CinemachineCameraTarget.transform;
            }
```

## Set Number Players In Room in UI : 
- Create a Network Variable store this value : 
```c# 
    private NetworkVariable<int> playersInRoom = new NetworkVariable<int>();
    public int PlayersInRoom
    {
        get { return playersInRoom.Value; }
    }
```
- Then when Start: Handle event OnClientConnected and Disconnected for changing number players in room like this : 
```c#
NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
```
- Excecute change playersInRoom value when in server : When on server change this value, all client gonna be change after.
```c# 
private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client ID {clientId} just Connected...");
        if (IsServer)
        {
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
```
- Finnaly, in Update we can update UI text to show : 
```c# 
    _amoutPlayerOnline.text = PlayersInRoom.ToString();
```
** The more simple way to show total clients connected : 
- On Start() : Add lines with func OnClientConnected, OnClientDisconnected are same as above: 
```c# 
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        if(IsServer){
            playersInRoom.Value ++;
        }
```
- Then in Update(), set UI text : 
    ```c# 
    _amoutPlayerOnline.text = PlayersInRoom.ToString();
    ```
## Đồng bộ tên của User qua NetCode : 
- Trong PlayerController (hoặc là file Player Logic của bạn, kế thừa NetworkBehaviour), tạo 1 biến `playerName` : 
```c# 
        public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>("No-name", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public string PlayerName {
            get {return playerName.Value.ToString();}
        }
        public TextMeshPro playerNameText;
```
- Bạn cũng có thể tạo 1 `struct` với tên là NetworkString và sử dụng nó thay cho `FixedString32Bytes`, sau đó là sử dụng `NetworkVarialbe` như là 1 biến string thông thường. [Link](https://youtu.be/rFCFMkzFaog?t=1010)

* Trong đoạn kế thừa func `OnNetworkSpawn()` 
```c# 
    /* Listen event when playerName changed value on server */
    playerName.OnValueChanged += OnPlayerNameChanged;
    /* Check if this client spawned, so set the player name notice to server */
    if(IsLocalPlayer){
        SetPlayerNameServerRpc(PlayerDataManager.Instance.playerData.name);
    }
```
- Tạo hàm SetPlayer ở phía ServerRpc và ClientRpc : 
```c# 
        [ServerRpc(RequireOwnership = false)]
        public void SetPlayerNameServerRpc(string name)
        {
            Debug.Log(" SetPlayerNameServerRpc : " + name);
            /* When Network Variable change in server, it'll trigger event, notify to all clients via event OnValueChanged */
            playerName.Value = new FixedString32Bytes(name);
        }
```
- Trong hàm lắng nghe thuộc tính `PlayerName` thay đổi : `OnPlayerNameChanged` : Ta thực hiện cập nhật lại tên player trên text ở UI. 
```c# 
      private void OnPlayerNameChanged(FixedString32Bytes previous, FixedString32Bytes current)
        {
            Debug.Log($"= ClientID {NetworkManager.LocalClientId} detect Player Name Change : {current}");
            playerNameText.text = current.ToString();
        }
```
- Quay lại với `Player Prefab`, tạo UI Text để hiển thị tên người chơi, sau đó kéo nó liên kết với biến `playerNameText` mà đã khai báo lúc trước.  
- Cơ bản đã thiết lập được tên người chơi : `StartHost` cùng với tên người chơi, Các người chơi khác vào game sẽ thấy tên của bạn.  
* Đến đây thấy có 1 vấn đề. Trong khung hình của người chơi vào sau cùng, tên của người chơi trước đó chưa được cập nhật. Tôi sẽ sửa nó trong bước tiếp theo. 
- Trong hàm `OnNetworkSpawn()`, thêm dòng bên dưới, nó sẽ thay đổi tên của khách đó và cập nhật thay đổi trên tất cả người chơi khác.  
```c# 
            if (IsOwner)
            {
                playerName.Value = new FixedString32Bytes(PlayerDataManager.Instance.playerData.name);
            }
```
- Trong hàm `Update()`, ta thiết lập tên hiển thị trên UI : 
        `playerNameText.text = PlayerName;`

## Quản lý danh sách các Player trong Play Scene :
- Trong `PlayManager.cs`, tạo biến lưu trữ các player đã được sinh ra : 
```c# 
    Dictionary<ulong, NetCodeThirdPersonController> playersList = new Dictionary<ulong, NetCodeThirdPersonController>();
    public Dictionary<ulong, NetCodeThirdPersonController> PlayersList { get => playersList; }
```
- Và sau đó tôi sẽ cập nhật lại giá trị của `playersList` ở trong player script (Trong TH của tôi thì script đó tên là `NetCodeThirdPersonController.cs`) :  
```c# 
public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                playerName.Value = new FixedString32Bytes(PlayerDataManager.Instance.playerData.name);
            }
            /* Add new player to list */
            PlayManager.Instance.PlayersList.Add(this.OwnerClientId, this);
            StartLocalPlayer();
        }
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            /* Remove player by clientID from list  */
            PlayManager.Instance.PlayersList.Remove(this.OwnerClientId);
        }
```
- Tiếp theo, trong `PlaySceneManager` thì `listPlayer` sẽ được tự động cập nhật thay đổi. Bạn có thể sử dụng danh sách player đó để hiển thị trên UI danh sách những người chơi trong game (VD: hiển thị tên, máu, điểm,... )
- Trong dự án này, tôi sẽ tạo 1 bảng trên UI để hiển thị danh sách tên các player đang trong room. Thực hiện tạo trên UI Editor trên `PlayScene`  
- Và cuối cùng, kéo liên kết từ UI vừa tạo vào, và thực hiện điền danh sách người chơi trong room vào bảng người chơi đó. Ví dụ ở đây tôi viết cập nhật danh sách thông tin người chơi trong room trong hàm `Update()`:  
```c# 
    void Update()
    {
        _amoutPlayerOnline.text = PlayersInRoom.ToString();
        int i = 0 ;
        foreach(KeyValuePair<ulong, NetCodeThirdPersonController> player in PlayersList){
            // Debug.LogWarning($"= Client ID {player.Key} has Name {player.Value.PlayerName}");
            if(i <= listPlayerNameText.Length){
                listPlayerNameText[i].text = string.Format("#{0}: {3} - {1} - ID : {2}", i+1, player.Value.PlayerName, player.Key.ToString(), player.Value.TypeInGame.ToString());
                listPlayerNameText[i].gameObject.SetActive(true);
                i++;
            }
        }
        for(int j = i; j <= listPlayerNameText.Length - 1; j++){
            listPlayerNameText[j].gameObject.SetActive(false);
        }
    }
```

## Thêm logic cho Game Hide and Seek : 
- Trong dự án này, chủ phòng sẽ được chọn làm cảnh sát và ghi điểm bằng cách bắt tất cả các kẻ cướp khác. Những người chơi còn lại sẽ là cướp và bắt đầu chạy khi được sinh ra. 
- Tạo `tag` Poilce và Thief.  
- Tạo biến để lưu loại của người chơi, để biến này trong file `NetCodeThirdPersonController.cs` :  
```c# 
private NetworkVariable<PlayerTypeInGame> typeInGame = new NetworkVariable<PlayerTypeInGame>(PlayerTypeInGame.Thief, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public PlayerTypeInGame TypeInGame
        {
            get { return typeInGame.Value; }
        }
```
- Trong `OnNetworkSpawn()`, thêm hàm lắng nghe khi `OnTypeInGame` có sự thay đổi giá trị, kiểm tra nếu là `IsOwner` và là `IsHost` thì Người chơi này chính là chủ phòng và sẽ được chọn làm Cảnh sát : Thay đổi `typeInGame.value = Police` và gắn tag Police cho Player này bằng đoạn code sau :  
```c# 
        public override void OnNetworkSpawn()
        {
            typeInGame.OnValueChanged += OnTypeInGameChange;
            if (IsOwner)
            {
                playerName.Value = new FixedString32Bytes(PlayerDataManager.Instance.playerData.name);
                /* Host create this room will be Police, and all next clients are thief */
                if (IsHost)
                {
                    typeInGame.Value = PlayerTypeInGame.Police;
                    this.tag = Constants.TAG_POLICE;
                }
                else
                {
                    typeInGame.Value = PlayerTypeInGame.Thief;
                    this.tag = Constants.TAG_THIEF;
                }
            }
            /* .... do st other */
        }
        public void OnTypeInGameChange(PlayerTypeInGame pre, PlayerTypeInGame current){
            this.tag = current.ToString(); /* Police or Thief */
        }
        public override void OnNetworkDespawn()
        { /* Remove listen when OnNetworkDespawn */
            base.OnNetworkDespawn();
            typeInGame.OnValueChanged -= OnTypeInGameChange;
            PlayManager.Instance.PlayersList.Remove(this.OwnerClientId);
        }
```
- Tạo 1 chút sự khác biệt giữa Cảnh sát và Cướp để nhận biết : Cơ bản ở đây tôi thay đổi màu của tên người chơi, Cảnh sát thì màu xanh, Cướp thì màu đỏ. Thực hiện thay đổi trong hàm `Update()`: 
```c# 
            if (TypeInGame == PlayerTypeInGame.Police)
            {
                playerNameText.color = Color.green;
            }
            else
            {
                playerNameText.color = Color.red;
            }
```

============================ LOGIC POLICE CHẠM VÀO THIEF ================= 
## Cơ chế khi Cảnh sát chạm vào Cướp : Hiện hiệu ứng, tính điểm, cho Cướp bất tử trong vài giây kể từ sau khi bị Cảnh sát chạm vào :"
### Thêm `EventManager` để quản lý các events trong game :  
- Thêm script `EventManager.cs` vào scene đầu tiên và chọn `isPersistance` để file đó sẽ tồn tại. Thêm sự kiện `EventName` có tên là `TouchThief`, tôi sẽ sử dụng nó sau này.
- Lưu ý rằng bạn nên vào Project Setting > Script Excecute Order và thiết lập thời gian cho `EventManager` để nó được chạy đầu tiên
- Để kiểm tra xem Cảnh sát đã chạm vào Cướp hay chưa, tôi đã tạo `tag` cho từng Player theo từng role (Police/Thief) của họ trước đó.
- Ta sẽ kiểm tra va chạm trong script `BasicRigidBodyPush.cs` ở trong `Player Prefab`. 
- Trong `BasicRigidBodyPush.cs`, viết hàm `OnControllerColliderHit` giống như bên dưới : 
```c# 
    private void OnControllerColliderHit(ControllerColliderHit hit){
        ...
        /* If not police, dont check collide */
        /* If not police, dont check collide */
        if (this.gameObject.tag != Constants.TAG_POLICE) return;

		/* If you are Police, let's check what you touch */
        if (hit.gameObject.tag == Constants.TAG_THIEF)
        {
            /* Touched to Thief , let's do something */
			NetCodeThirdPersonController target = hit.gameObject.GetComponent<NetCodeThirdPersonController>();
			Debug.Log("Touch to Thief : IsImmortal : " + target.IsImmortal.ToString());
			/* Firstly check if this thief is in immortal state -> do nothing
			If are playing as normal, trigger event that police touch this thief and do some logic */
			if(!target.IsImmortal){
				/* Call func ON Touch Thief. */
				this.gameObject.GetComponent<NetCodeThirdPersonController>().OnTouchThief(target);
				
			}
			
        }
    }
```
- Đến đây, chúng ta sẽ bắn sự kiến với người chơi này để họ biết họ đang chạm vào Cướp và 1 số thông tin định danh tên cướp đó thông qua `NetCodeThirdPersonController.cs` 
- Trong NetCodeThirdPersonController.cs, ta thêm đoạn code sau : 
```c# 

        #region  Game Logic 
        /* Listen event TouchThief and ready to make notify to server know that I've catched a thief */
        public void OnTouchThief(NetCodeThirdPersonController target)
        {
            Debug.Log($"= Event OnTouchThief : I'm {PlayerName} - ID {OwnerClientId} and I catched a thief has name is {target.PlayerName} - ID: {target.OwnerClientId}");

            /* Call to ServerRpc to notify excute explosion effect for all clients */
            OnPoliceCatchedThiefServerRpc(target.OwnerClientId);
        }
```
- ĐƯợc rồi. Giờ chúng ta sẽ build ra và khởi chạy 2 game, 2 người chơi là Cảnh sát và Cướp. Và bất cứ khi nào Cảnh sát chạm vào Cướp thì ở trong Log bạn sẽ thấy hiện dòng thông báo. 
### Hiện hiệu ứng vụ nổ và thông báo tới Server rằng tất cả các người chơi biết được ai đó đã bị chạm :  
- Trong hàm `OnTouchThief()` ta tạo ở bên trên, ta sẽ thực hiện gọi ServerRPC để thông báo cho server biết, và từ server sẽ thông báo tới tất cả clients : 
```c# 
    public void OnTouchThief(Dictionary<string, object> msg){
        ...
        /* Call to ServerRpc to notify excute explosion effect for all clients */
            OnPoliceCatchedThiefServerRpc(OwnerClientId, target.OwnerClientId);
    }
```
- Tạo hàm  `OnPoliceCatchedThiefServerRpc()` như sau: 
```c# 
    [ServerRpc]
        public void OnPoliceCatchedThiefServerRpc(ulong fromClientId, ulong targetClientId, ServerRpcParams serverRpcParams = default){
            /* We have 2 ways to this thing : Choose one and comment the other one */
            
            /* Option 1: Spawn on server, so all clients automacally spawn this effect : But got error when you trying destroy this object from clients */
            GameObject explosionVfx = Instantiate(PlayManager.Instance.explosionBoomPrefab);
            explosionVfx.GetComponent<NetworkObject>().Spawn();
            explosionVfx.transform.position = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject.transform.position;

            /* Option 2: Notify for all client know where explosion happend and act it on client : So you can control and destroy this object */
            ShowExplosionEffectInClientRpc(targetClientId);
        }
```
- Trong đoạn code trên tôi có chia làm 2 cách, tôi sẽ sử dụng cách 2 bởi vì vụ nổ chỉ mang tính hiển thị và không ảnh hướng tới tính logic của game.  
- Tiếp theo tạo hàm `ShowExplosionEffectInClientRpc()` để nhận thông báo hiển thị hiệu ứng từ server. Về prefab Vụ nổ thì bạn tự kiếm nhé hoặc lấy trong project này :  
```c# 
        [ClientRpc]
        public void ShowExplosionEffectInClientRpc(ulong targetClientId){
            /* Receive info from Server and perform explosion in client */
            GameObject explosionVfx = Instantiate(PlayManager.Instance.explosionBoomPrefab);
            explosionVfx.transform.position = PlayManager.Instance.PlayersList[targetClientId].gameObject.transform.position;
            /* I've set auto destroy this particle system when it's done.  */
        }
```
- Mọi thứ có vẻ ổn ôn rồi. Có thể build và khởi chạy 2 màn hình game để kiểm tra va chạm, vụ nổ giữa Cảnh sát và Cướp

### Logic cho Cướp bất tử trong vòng 3 giây sau khi bị Cảnh sát bắt :  
- Tạo 1 biến `isImmortal` trong `NetCodeThirdPersonController.cs`: 
```c# 
    [Tooltip("isImmortal : true -> police cannot catch this thief when touch. This variable just change on ServerRpc. Don't trust client")]
        private NetworkVariable<bool> isImmortal = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public bool IsImmortal { get { return isImmortal.Value; } }
```
- Lắng nghe sự kiện khi biến `isImmortal` thay đổi giá trị trong hàm `OnNetworkSpawn()` và `OnNetworkDespawn()` : 
```c# 
        base.OnNetworkSpawn();
        isImmortal.OnValueChanged += OnIsImmortalChange;
        ...
        base.OnNetworkDespawn();
        isImmortal.OnValueChanged -= OnIsImmortalChange;

        /* Cause I change isImmortal in server so in this func just using for Logging */
        public void OnIsImmortalChange(bool pre, bool current)
        {   
            if(!IsOwner) return; /* If it's not owner, do nothing */
            Debug.Log($"= OnIsImmortalChange Client Name {PlayerName} ID {NetworkManager.LocalClientId} change isImmortal from {pre.ToString()} to {current.ToString()}");
        }
```
- Bây giờ, ta sẽ lựa chọn xem khi nào thì sẽ thay đổi giá trị của `isImmortal`. Nó sẽ thay đổi trên server khi mà kẻ cướp đó bị Cảnh sát chạm vào. 
- Quay trở lại hàm `OnPoliceCatchedThiefServerRpc()`, và thêm đoạn code sau để Cướp có thể bất tử : 
```c# 
    public void OnPoliceCatchedThiefServerRpc(ulong targetClientId, ServerRpcParams serverRpcParams = default){
        ...
            /* Set target Client immortal in some seconds */
            NetCodeThirdPersonController targetPlayer = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject.GetComponent<NetCodeThirdPersonController>();
            targetPlayer.isImmortal.Value = true;
            StartCoroutine(IESetImmortalFalse(targetPlayer, 3f)); /* delay 3 seconds before change isImmortal to false */
    }
    public IEnumerator IESetImmortalFalse(NetCodeThirdPersonController targetPlayer, float delay)
    {
        Debug.Log($"= IESetImmortalFalse Client Name {targetPlayer.PlayerName} Id {targetPlayer.OwnerClientId} start Coroutine change isImmortal to false");
        yield return new WaitForSeconds(delay);
        targetPlayer.isImmortal.Value = false;
    }
``` 
- Chạy thử game thôi. 


### Logic tính điểm : Tăng/giảm điểm khi Cảnh sát chạm vào cướp:  
- Theo ý tưởng của tôi, thì có 2 hướng: Thay đổi điểm và cập nhật trong `Update()` hoặc thay đổi điểm và lắng nghe sự kiến `OnValueChange` của điểm rồi hiển thị lên UI. Ở đây tôi sử dụng cách 1. 
- Khai báo biến `Point` để lưu trữ điểm của mỗi Player 
```c# 
       /* Point to count the game logic : Police touch thief -> police's point ++ , thief's point -- */
        private NetworkVariable<int> point = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public int Point {
            get { return point.Value;}
        }
```
- Khi Cảnh sát chạm vào Cướp, thực hiện 1 số logic trên ServerRpc, và ở đây, tôi sẽ tính toán điểm như sau :  
```c# 
        [ServerRpc(RequireOwnership = false)]
        public void OnPoliceCatchedThiefServerRpc(ulong targetClientId, ServerRpcParams serverRpcParams = default){
        ...
        /* Logic Increase Police's point, Decrease Thief's point */
            targetPlayer.point.Value --;
            senderPlayer.point.Value ++;

        }
```
- Tiếp đến quay lại với `PlaySceneManager.cs` và thêm dòng điểm vào trong bảng danh sách thông tin các người chơi trong phòng.   
```c# 
    ...
    listPlayerNameText[i].text = string.Format("#{0}: {3} - {1} - ID : {2} - P : {4}", i+1, player.Value.PlayerName, player.Key.ToString(), player.Value.TypeInGame.ToString(), player.Value.Point);
```

### Logic sinh ra các Vật phẩm trong game : Vật phẩm cho Cảnh sát và cho Cướp :  
- Vật phẩm Cảnh sát sẽ có màu xanh. Khi cảnh sát chạm vào, điểm của họ sẽ được tăng.  
- Vật phẩm Cướp sẽ có màu đỏ. Khi Cướp chạm vào, điểm của họ sẽ được tăng.  

- Quay trở lại `PlayScene`, tạo Vật phẩm : đầu tiên tạo 1 `Cube`, và tuỳ chỉnh hình dạng, màu sắc cho chúng. Ở đây tôi chỉnh màu, animation để nó quay quay vòng tròn, và gắn `Tag` `Police Bonus` hoặc `Thief Bonus` cho chúng. Tick chọn `IsTrigger` trong `Box Collider`, thay đổi tỷ lệ cho phù hợp với màn chơi, và thêm Component `NetworkObjectComponent` và trong cube này. 
- Kéo nó thành `prefab` ở trong folder `Resources`, và thêm nó vào trong `Network Prefabs List` ở trong NetworkManager. 
- Tạo 1 file script tên `BonusItem.cs` để tạo các thuộc tính cho `Bonus Prefab`. Nó bao gồm các thuộc tính của Vật phẩm tăng cường đó. Thêm script này vào trong `Bonus Prefab`.
```c# 
    [Serializable]
    public class BonusData { 
        /* Type of this bonus is using for what character : Police or Thief */
        public BonusType bonusType = BonusType.Police;
        /* This value using to represents the value of increase and decrease. eg: Police speed increase [value], Thief increase point equal [value] */
        public int value = 1;
    }
```
- Thêm logic để kiểm tra Người chơi va chạm với Vật phẩm tăng cường trong `NetCodeThirdPersonController.cs`, Cảnh sát chỉ có thể va chạm với Vật phẩm của Cảnh sát, Cướp va chạm với Vật phẩm của Cướp. 
```c#
        void OnTriggerEnter(Collider other)
        {
            /* If not Owner, don't do anything. If not add this line, other client in your side also come here */
            if(!IsOwner) return;
            
            BonusItem target = other.GetComponent<BonusItem>();
            
            /* if This is Police and touch to Police Bonus */
            if(target && target.bonusData.bonusType == BonusType.Police && TypeInGame == PlayerTypeInGame.Police){
                ulong bonusId = target.GetComponent<NetworkObject>().NetworkObjectId;
                Debug.Log($"== OnTriggerEnter with : {target.bonusData.bonusType} has NetworkObjectId : {bonusId}");
                PlayManager.Instance.PoliceTouchedPoliceBonusServerRpc(bonusId);
            }
            /* if This is Thief and touch to Thief Bonus */
            if(target && target.bonusData.bonusType == BonusType.Thief && TypeInGame == PlayerTypeInGame.Thief){
                ulong bonusId = target.GetComponent<NetworkObject>().NetworkObjectId;
                Debug.Log($"== OnTriggerEnter with : {target.bonusData.bonusType} has NetworkObjectId : {bonusId}");
                PlayManager.Instance.ThiefTouchedThiefBonusServerRpc(bonusId);
            }
        }
```

- Tạo danh sách chứa các Vật phẩm đã được sinh ra trong Scene : Tôi sẽ thực hiện sinh ra các Vật phẩm tại các vị trí ngẫu nhiên
- Khởi tạo `ArrayList` để lưu danh sách các Vật phẩm và tôi đã sinh ra trước đó :  
```c# 
    [SerializeField] private Transform[] listSpawnBonusPosition; /* List positions could be choose for spawn new Bonus */
    [SerializeField] private GameObject policeBonusPrefab; 
    [SerializeField] private GameObject thiefBonusPrefab; 
    [SerializeField] private int maxPoliceBonus;  /* Max Police Bonus could be spawn in game */
    [SerializeField] private int maxThiefBonus; /* Max Thief Bonus could be spawn in game */
    private List<ulong> listPoliceBonusIdSpawned = new List<ulong>(); /* Store List Police Bonus are spawned in game */
    private List<ulong> listThiefBonusIdSpawned = new List<ulong>(); /* Store List Thief Bonus are spawned in game */
```
- Trong hàm `Start()` trong `PlaySceneManager.cs`, kiểm tra nếu là Server thì thực hiện sinh ra Vật phẩm ở trong map. Sau khi sinh ra, đừng quên lưu lại `networkObjectId` vào danh sách, nó sẽ giúp định danh được các đối tượng.
- Tiếp đến tạo hàm xử lý khi Cảnh sát/Cướp chạm được vào vật phẩm của họ, thực hiện tăng điểm, xoá vật phẩm đó khỏi danh sách, huỷ vật phẩm đó và gọi hàm để tính toán sinh ra vật phẩm mới trong map :
```c# 
    void Start(){
    ...
        if(IsServer){
                SpawnBonusPrefabServerRpc(); 
        }
    ...
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
    }
    #endregion
```

- Đừng quên để `RequireOwnership = false`, khi đó thì Các người chơi khi chạm vào bonus đều có thể gọi tới Server để thực hiện logic.  

### Change a bit logic when touch Bonus : Increase their Point : 
- For more simplier game play, so I've changed logic game when Police/Thief touched their Bonus Item, so I'll increase their Point : Add these lines to func touched Bonus in server : 
```c# 
        /* Increase Point for Police */
        NetCodeThirdPersonController sender = PlayersList[senderId];
        if(sender != null){
            sender.point.Value += bonusItem.GetComponent<BonusItem>().bonusData.value;
        }
```
- Get back to Play Scene and adjust max bonus can spawn of police, reduce it to smaller than max bonus can spawn of thief for balance game I think so because when Police touched Thief, Thief's point decrease and Police's point increase also. 
- OK So we're basically done game logic, so host and client can go into a room and chasing each other, take the bonus, get the point and let's see who has the best point when end the game. 

## Popup End Game :
- In this project, I'll make simple logic end game. That's when any player reach max goal point, the game'll be, show Popup result. 
- First of all, you'll need create a UI for Popup End Game like this : 
![img.png](Images/ui_popup_endgame.png)

- In `PlayManager.cs`, we should create some variables : 
```c#
    ...
        [Header("Popup End Game")]
    [SerializeField] private GameObject _popupEndGame; // GameObject of Popup End Game content.
    [SerializeField] private Transform _listPlayerEndGameTransform;
    [SerializeField] public bool _isEndGame = false; // true when game has end.  
    [SerializeField] public int _pointEndGame; // End game when one of player reach this point.
    [SerializeField] private TextMeshProUGUI[] listEndGamePlayerNameText;
    
    // When Start(), disable popup end game. 
    void Start()
    {
        _popupEndGame.SetActive(false);
        _isEndGame = false;
        ...
```

- In `NetCodeThirdPersonController.cs`, we add some logic : Listen event Point change value and check if point has reach max point, call to ServerRpc to notice that one of player reached max point. 
- And then, In ServerRpc'll broadcast to all clients know that One of player has reached max point and show popup end game. 
```c#

      public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ...
            point.OnValueChanged += OnPointChange; // Dont forget remove OnValueChanged in OnNetworkDespawn.
            ...
        }
        /* Catch event when this point has changed */
        public void OnPointChange(int pre, int current)
        {
            if (!IsOwner) return;
            if (current >= PlayManager.Instance._pointEndGame)
            {
                Debug.Log($"$This Player {PlayerName} has reach End Game Point. Show Endgame now...");
                PlayManager.Instance.ShowPopupEndGameServerRpc();
            }
        }

        private void Update(){
            // stop every actions when isEndGame = true
            if (PlayManager.Instance._isEndGame) return;
            ...
        }
```
- Get back to `PlayManager.cs`. I'll create func ServerRpc, check show Popup End Game in here: 
```c#
     [ServerRpc(RequireOwnership = false)]
    public void ShowPopupEndGameServerRpc(ServerRpcParams serverRpcParams = default)
    {
        Debug.Log("== ShowPopupEndGameServerRpc Trigged. Broadcast event end game to all clients");
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
        /* Fill list player data and score to table list player */
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
```


## Sign in, Login via UGS Authentication : 
- Go to Project Settings > Services and link to your project in Unity Dashboard. 
- Go to Unity Dashboard and Setup Authentication. 
- So in Unity Editor > Project Settings > Services > Authentication : You'll see the list user accounts. 
- Create UI Menu for Sign in : Text name, button sign in, status text. 
- In MenuSceneManager.cs, add this function to Sign in : 
```c# 
    using Unity.Services.Authentication;
    using Unity.Services.Core;
    using Unity.Services.Lobbies;
    ...
    public async void SignInButtonClicked(){
            if (string.IsNullOrEmpty(_nameTextField.text))
            {
                Debug.Log($"Signing in with the default profile");
                await UnityServices.InitializeAsync();
            }
            else
            {
                Debug.Log($"Signing in with profile '{_nameTextField.text}'");
                var options = new InitializationOptions();
                options.SetProfile(_nameTextField.text);
                await UnityServices.InitializeAsync(options);
            }

            try
            {
                signInButton.interactable = false;
                statusText.text = $"Signing in .... ";
                AuthenticationService.Instance.SignedIn += delegate
                {
                    PlayerDataManager.Instance.SetId(AuthenticationService.Instance.PlayerId);
                    UpdateStatusText();
                    profileMenu.SetActive(false);
                    lobbyMenu.SetActive(true);
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
```
- In function, if you lack of what variable, just declare and refers it. 
- In Awake(), Init Unity Services : 
```c# 
    UnityServices.InitializeAsync();
```
- For the case comback from Play Scene to Menu Scene, let's check User signed in or not. So in MenuSceneManager,in Start() func call CheckAuthentication() : 
```c# 
    void CheckAuthentication()
    {
        /* Check signed in */
        if (AuthenticationService.Instance.IsSignedIn)
        {
            UpdateStatusText();
            profileMenu.SetActive(false);
            lobbyMenu.SetActive(true);
        }
        else
        {
            profileMenu.SetActive(true);
            lobbyMenu.SetActive(false);
        }
    }
```

## Create Lobby :
- Import Unity package Lobby in Unity Registry.  
- Create UI Lobby, Show it after signed in.
- Setup Lobby Project : Lobby Document [Link](https://docs.unity.com/ugs/en-us/manual/lobby/manual/get-started)
- See more in STEP 2 - Tashi.md.


