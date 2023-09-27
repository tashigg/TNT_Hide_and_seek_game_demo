using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif
using Unity.Netcode;
using System;
using Random = UnityEngine.Random;
using TMPro;
using Unity.Collections;
using System.Collections.Generic;
using System.Collections;

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class NetCodeThirdPersonController : NetworkBehaviour
    {
        [Header("Player")]
        public PlayerData playerData = new PlayerData();
        [Tooltip("isImmortal : true -> police cannot catch this thief when touch. This variable just change on ServerRpc. Don't trust client")]
        private NetworkVariable<bool> isImmortal = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public bool IsImmortal { get { return isImmortal.Value; } }
        public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>("No-name", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public string PlayerName
        {
            get { return playerName.Value.ToString(); }
        }
        private NetworkVariable<PlayerTypeInGame> typeInGame = new NetworkVariable<PlayerTypeInGame>(PlayerTypeInGame.Thief, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public PlayerTypeInGame TypeInGame
        {
            get { return typeInGame.Value; }
        }
        /* Point to count the game logic : Police touch thief -> police's point ++ , thief's point -- */
        public NetworkVariable<int> point = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public int Point
        {
            get { return point.Value; }
        }

        public TextMeshPro playerNameText;
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Tooltip("Adjust mouse rotation sensitive")]
        public float LookSensitivity = 1f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
                try
                {
                    if (_playerInput == null)
                    {
                        // Debug.LogWarning("== Player Input is null"); /* Alwayls show in clients, have to using joystick to move */
                    }
                    return _playerInput.currentControlScheme == "KeyboardMouse";
                }
                catch (Exception e)
                {
                    return false;
                }
#else
                return false;
#endif
            }
        }
        void OnEnable()
        {
            // EventManager.Instance.StartListening(EventName.TouchThief, OnTouchThief);
        }
        void OnDisable()
        {
            // EventManager.Instance.StopListening(EventName.TouchThief, OnTouchThief);
        }

        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }

        }

        private void Start()
        {
            Debug.Log("=== Start NetCode Third Person Controller OnNetworkSpawn ID: " + OwnerClientId + " Role : " + (IsHost ? "Host" : "Client"));
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

            AssignAnimationIDs();

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

        }
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            typeInGame.OnValueChanged += OnTypeInGameChange;
            isImmortal.OnValueChanged += OnIsImmortalChange;
            point.OnValueChanged += OnPointChange;
            if (IsOwner)
            {
                playerName.Value = new FixedString32Bytes(PlayerDataManager.Instance.playerData.name);
                /* Host create this room will be Police, and all next clients are thief */
                if (IsHost)
                {
                    typeInGame.Value = PlayerTypeInGame.Police;
                    this.tag = Constants.TAG_POLICE;
                    this.transform.position = PlayManager.Instance.policeSpawnTransform.position;
                }
                else
                {
                    typeInGame.Value = PlayerTypeInGame.Thief;
                    this.tag = Constants.TAG_THIEF;
                    this.transform.position = PlayManager.Instance.thiefSpawnTransform.position;
                }
            }

            PlayManager.Instance.PlayersList.Add(this.OwnerClientId, this);  /* Move : Add when Server Scene Init in Play Manager*/
            StartLocalPlayer();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            typeInGame.OnValueChanged -= OnTypeInGameChange;
            isImmortal.OnValueChanged -= OnIsImmortalChange;
            point.OnValueChanged -= OnPointChange;
            PlayManager.Instance.PlayersList.Remove(this.OwnerClientId);
        }
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

        private void Update()
        {
            if (PlayManager.Instance._isEndGame) return;
            playerNameText.text = PlayerName;
            if (TypeInGame == PlayerTypeInGame.Police)
            {
                playerNameText.color = Color.green;
            }
            else
            {
                playerNameText.color = Color.red;
            }
            if (IsOwner)
            {
                if(_playerInput == null){
                    Debug.LogWarning("= Onwer Player Input Null");
                }
                _hasAnimator = TryGetComponent(out _animator);

                JumpAndGravity();
                GroundedCheck();
                Move();
            }
        }

        private void LateUpdate()
        {
            if(!IsOwner) return;
            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier * LookSensitivity;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier * LookSensitivity;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }

        #region Network Variable On Change Value 
        public void OnTypeInGameChange(PlayerTypeInGame pre, PlayerTypeInGame current)
        {
            this.tag = current.ToString(); /* Police or Thief */
        }
        /* Cause I change isImmortal in server so in this func just using for Logging */
        public void OnIsImmortalChange(bool pre, bool current)
        {
            if (!IsOwner) return; /* If it's not owner, do nothing */
            Debug.Log($"= OnIsImmortalChange Client Name {PlayerName} ID {NetworkManager.LocalClientId} change isImmortal from {pre.ToString()} to {current.ToString()}");
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

        #endregion

        #region  Game Logic 
        /* Listen event TouchThief and ready to make notify to server know that I've catched a thief */
        public void OnTouchThief(NetCodeThirdPersonController target)
        {
            Debug.Log($"= Event OnTouchThief : I'm {PlayerName} - ID {OwnerClientId} and I catched a thief has name is {target.PlayerName} - ID: {target.OwnerClientId}");

            /* Call to ServerRpc to notify excute explosion effect for all clients */
            OnPoliceCatchedThiefServerRpc(target.OwnerClientId);
        }
        public IEnumerator IESetImmortalFalse(NetCodeThirdPersonController targetPlayer, float delay)
        {
            Debug.Log($"= IESetImmortalFalse Client Name {targetPlayer.PlayerName} Id {targetPlayer.OwnerClientId} start Coroutine change isImmortal to false");
            yield return new WaitForSeconds(delay);
            targetPlayer.isImmortal.Value = false;

        }
        public IEnumerator IEDespawnNetworkObject(float delay, GameObject target)
        {
            yield return new WaitForSeconds(delay);
            Destroy(target);

        }
        #endregion
        #region ServerRpc function
        [ServerRpc(RequireOwnership = false)]
        public void OnPoliceCatchedThiefServerRpc(ulong targetClientId, ServerRpcParams serverRpcParams = default)
        {
            var clientId = serverRpcParams.Receive.SenderClientId;
            NetCodeThirdPersonController senderPlayer = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponent<NetCodeThirdPersonController>();
            Debug.Log($"= OnPoliceCatchedThiefServerRpc : Client : {serverRpcParams.Receive.SenderClientId} has sent to ServerRpc target to ClientID : {targetClientId}");
            /* Option 1: Spawn on server */
            // GameObject explosionVfx = Instantiate(PlayManager.Instance.explosionBoomPrefab);
            // explosionVfx.GetComponent<NetworkObject>().Spawn();
            // explosionVfx.transform.position = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject.transform.position;

            /* Option 2: Notify for all client know where explosion happend and act it on client */
            ShowExplosionEffectInClientRpc(targetClientId);

            /* Make thief immortal for a time */
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };

            /* Set target Client immortal in some seconds */
            NetCodeThirdPersonController targetPlayer = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject.GetComponent<NetCodeThirdPersonController>();
            targetPlayer.isImmortal.Value = true;
            StartCoroutine(IESetImmortalFalse(targetPlayer, 3f));

            /* Logic Increase Police's point, Decrease Thief's point */
            targetPlayer.point.Value -= 2;
            senderPlayer.point.Value += 2;
        }

        #endregion
        #region ClientRpc function
        [ClientRpc]
        private void SetIsImmortalClientRpc(bool value, ClientRpcParams clientRpcParams = default)
        {
            /* If IsOwner so this func will exceute right on ServerRpc, so don't need run more time */
            Debug.Log($"= SetIsImmortalClientRpc This Player {PlayerName} has ID : Local CLientID  {NetworkManager.LocalClientId}");

            Debug.Log($"= SetIsImmortalClientRpc 2 This Player {PlayerName} has SetIsImmortal to {value.ToString()}");
            isImmortal.Value = value;

        }
        [ClientRpc]
        private void ShowExplosionEffectInClientRpc(ulong targetClientId)
        {
            /* Receive info from Server and perform explosion in client */
            GameObject explosionVfx = Instantiate(PlayManager.Instance.explosionBoomPrefab);
            explosionVfx.transform.position = PlayManager.Instance.PlayersList[targetClientId].gameObject.transform.position + new Vector3(0f, 0.5f, 0f);
            /* I've set auto destroy this particle system when it's done.  */

            /* Check if  */
            if (OwnerClientId == targetClientId)
            {

            }
        }
        #endregion
        #region Event Function

        #endregion

        /// <summary>
        /// OnTriggerEnter is called when the Collider other enters the trigger.
        /// </summary>
        /// <param name="other">The other Collider involved in this collision.</param>
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
    }
}