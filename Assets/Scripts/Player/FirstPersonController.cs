using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
#endif
namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour
    {
        public static FirstPersonController instance;

        [Header("Player")]
        public float MoveSpeed = 4.0f;
        public float SprintSpeed = 6.0f;
        public float RotationSpeed = 1.0f;
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;

        [Space(10)]
        public float JumpTimeout = 0.1f;
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.5f;
        public LayerMask GroundLayers;

        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private Animator _animator;
        private AudioSource _audioSource;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        [Header("playerStats")]
        [SerializeField] private float PlayerHealth = 100;
        [SerializeField] private float MaxHealth = 100f;
        [SerializeField] private int Defense = 1;
        private bool isAlive = true;

        [SerializeField] private float PlayerStamina = 100f;
        [SerializeField] private float MaxStamina = 100f;
        [SerializeField] private float StaminaDrainRate = 15f;
        [SerializeField] private float StaminaRegenRate = 10f;
        private bool isSprinting = false;

        [SerializeField] private float StaminaCooldownDuration = 2f;
        private bool staminaExhausted = false;
        private float staminaCooldownTimer = 0f;

        [Header("UI stats")]
        private Slider healthbar;
        private Slider staminaSlider;
        private TextMeshProUGUI armorRating;

        [Header("Camera")]
        [SerializeField] private Transform CameraRoot;
        [SerializeField] private Transform Camera;

        [SerializeField] private float UpperLimit = -40f;
        [SerializeField] private float LowerLimit = 70f;
        [SerializeField] private float MouseSensitivity = 21.9f;
        private float _xRot;

        [Header("Combat")]
        [SerializeField] private float attackCooldown = 2.0f;
        [SerializeField] private float attackDistance = 3f;
        public int attackDamage = 1;

        [SerializeField] private AudioClip attackSFX;
        [SerializeField] private AudioClip hitSFX;
        [SerializeField] private AudioClip deathSFX;
        int attackcount;

        private float _lastAttackTime = -999f;

        [Header("Respawn settings")]
        [SerializeField] private bool enableRespawn = true;
        [SerializeField] private float respawnHoldTime = 0.75f;
        [SerializeField, Range(0f, 1f)] private float respawnHealthPct = 1f;

        [SerializeField] private float deathFadeDuration = 2.5f;
        [SerializeField] private string deathStateTag = "Death";
        [SerializeField] private float fadeInDuration = 1.2f;

        private Vector3 fallbackSpawnPos;
        private Quaternion fallbackSpawnRot;
        private bool isRespawning = false;

        [Header("Inventory UI")]
        [SerializeField] private GameObject inventoryUI;
        public bool isInventoryOpen = false;

        [Header("Map UI")]
        [SerializeField] private GameObject MapUI;
        public bool isMapOpen = false;

        [SerializeField] private AudioClip openUISFX;

        [Header("Crosshairs")]
        [SerializeField] private GameObject normalCrosshair;
        [SerializeField] private GameObject stealCrosshair;
        [SerializeField] private GameObject collectCrosshair;
        [SerializeField] private float interactRange = 3f;

        [Header("equipment manager")]
        public Transform weaponHolder;
        public Transform[] armorHolder = new Transform[4];

        private int _uiLocks = 0;
        private bool _lockedByDialogue = false;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            instance = this;
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError("Starter Assets dependencies missing. Use Tools/Starter Assets/Reinstall Dependencies.");
#endif

            healthbar = GameObject.Find("HealthBar")?.GetComponent<Slider>();
            if (healthbar)
            {
                healthbar.maxValue = PlayerHealth;
                healthbar.value = PlayerHealth;
            }

            staminaSlider = GameObject.Find("Sprint")?.GetComponent<Slider>();
            if (staminaSlider)
            {
                staminaSlider.maxValue = MaxStamina;
                staminaSlider.value = PlayerStamina;
            }

            _animator = GetComponentInChildren<Animator>();
            _audioSource = GetComponent<AudioSource>();
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            fallbackSpawnPos = transform.position;
            fallbackSpawnRot = transform.rotation;

            DialogueService.OnOpen += HandleDialogueOpen;
            DialogueService.OnClose += HandleDialogueClose;
            ShopService.OnShopOpened += HandleShopOpen;
            ShopService.OnShopClosed += HandleShopClose;
        }

        private void OnDestroy()
        {
            DialogueService.OnOpen -= HandleDialogueOpen;
            DialogueService.OnClose -= HandleDialogueClose;
            ShopService.OnShopOpened -= HandleShopOpen;
            ShopService.OnShopClosed -= HandleShopClose;
        }

        private void Update()
        {

            if (isRespawning || _controller == null || !_controller.enabled || !gameObject.activeInHierarchy)
            {
                UpdateUI();
                return;
            }

            JumpAndGravity();
            GroundedCheck();
            
            Move();
            Attack();
            UpdateUI();

            if (_input.openInventory)
            {
                ToggleInventory();
                _input.openInventory = false;
            }

            if (_input.openMap)
            {
                ToggleMap();
                _input.openMap = false;
            }

            if (_input.interact)
            {
                Interact();
                _input.interact = false;
            }

            if (PlayerHealth > MaxHealth)
                PlayerHealth = MaxHealth;

            if (healthbar) healthbar.maxValue = MaxHealth;

            if (IsUiLocked())
            {
                _input.attack = false;
                _input.jump = false;
            }
        }

        private void LateUpdate()
        {
            if (isRespawning) return;
            CameraMovement();
        }

        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }

        public void PushUiLock()
        {
            _uiLocks++;
            _input.attack = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void PopUiLock()
        {
            _uiLocks = Mathf.Max(0, _uiLocks - 1);
            if (_uiLocks == 0)
            {
                Cursor.lockState = isInventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = isInventoryOpen;
            }
        }

        private bool IsUiLocked() => _uiLocks > 0;

        private void HandleDialogueOpen(DialogueDefinition d, DialogueNode n)
        {
            _lockedByDialogue = !DialogueService.IsAutoClosing;
            if (_lockedByDialogue)
                PushUiLock();
        }

        private void HandleDialogueClose()
        {
            if (_lockedByDialogue)
                PopUiLock();
            _lockedByDialogue = false;
        }

        private void HandleShopOpen(Trader t)
        {
            PushUiLock();
        }

        private void HandleShopClose()
        {
            PopUiLock();
        }

        private void UpdateUI()
        {
            if (healthbar) healthbar.value = PlayerHealth;
            if (staminaSlider) staminaSlider.value = PlayerStamina;

            UpdateLookHints();
        }

        private void ToggleInventory()
        {
            bool opening = !isInventoryOpen;
            if (opening && openUISFX) _audioSource.PlayOneShot(openUISFX);
            if (opening)
            {
                DialogueService.End();
                ShopService.End();
            }

            isInventoryOpen = opening;
            if (inventoryUI) inventoryUI.SetActive(isInventoryOpen);

            Cursor.lockState = isInventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isInventoryOpen;

            _input.cursorLocked = !isInventoryOpen;
        }

        private void Interact()
        {
            if (IsUiLocked()) return;

            Ray ray = new Ray(Camera.position, Camera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                var dlgComp = hit.collider.GetComponentInParent<DialogueComponent>();
                if (dlgComp != null)
                {
                    dlgComp.TryStartDialogue();
                    return;
                }

                var trader = hit.collider.GetComponentInParent<Trader>();
                if (trader != null)
                {
                    bool opened = trader.TryOpenShop(this);
                    if (!opened && !string.IsNullOrEmpty(trader.LastRefusalText))
                        DialogueService.BeginOneLiner(trader.name, trader.LastRefusalText, dlgComp);
                    return;
                }
                var chest = hit.collider.GetComponentInParent<SecretChest>();
                if (chest != null && chest.CanInteract())
                {
                    chest.Interact();
                    return;
                }

                var pickup = hit.collider.GetComponent<ItemCollection>();
                if (pickup != null)
                {
                    if (pickup.isOwned && !pickup.wasStolen)
                        pickup.MarkAsStolen();

                    InventoryManager.Instance.AddItem(pickup.itemData, pickup.amount);
                    Destroy(hit.collider.gameObject);
                }
            }
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
        }

        private void CameraMovement()
        {
            if (!_animator) return;

            var Mouse_X = _input.look.x;
            var Mouse_Y = _input.look.y;
            Camera.position = CameraRoot.position;
            Camera.rotation = CameraRoot.rotation;

            _xRot += Mouse_Y * MouseSensitivity * Time.deltaTime;
            _xRot = Mathf.Clamp(_xRot, UpperLimit, LowerLimit);
            Camera.localRotation = Quaternion.Euler(_xRot, 0, 0);

            float yaw = Mouse_X * MouseSensitivity * Time.deltaTime;
            Quaternion deltaRotation = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation *= deltaRotation;
        }

        private void Move()
        {
            if (_controller == null || !_controller.enabled || !gameObject.activeInHierarchy)
            {
                if (_animator) _animator.SetFloat("Running", 0f);
                return;
            }

            if (IsUiLocked())
            {
                _controller.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
                if (_animator) _animator.SetFloat("Running", 0f);
                return;
            }

            if (staminaExhausted)
            {
                staminaCooldownTimer -= Time.deltaTime;
                if (staminaCooldownTimer <= 0f && PlayerStamina > 10f)
                    staminaExhausted = false;
            }

            bool wantsToSprint = _input.sprint && !staminaExhausted && PlayerStamina > 0 && _input.move != Vector2.zero;
            isSprinting = wantsToSprint;

            float targetSpeed = isSprinting ? SprintSpeed : MoveSpeed;

            if (isSprinting)
            {
                PlayerStamina -= StaminaDrainRate * Time.deltaTime;
                if (PlayerStamina <= 0f)
                {
                    PlayerStamina = 0f;
                    staminaExhausted = true;
                    staminaCooldownTimer = StaminaCooldownDuration;
                    isSprinting = false;
                }
            }
            else
            {
                PlayerStamina += StaminaRegenRate * Time.deltaTime;
                if (PlayerStamina > MaxStamina) PlayerStamina = MaxStamina;
            }

            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            if (_input.move != Vector2.zero)
            {
                Vector3 forward = Camera.forward;
                Vector3 right = Camera.right;
                forward.y = 0f; right.y = 0f;
                forward.Normalize(); right.Normalize();
                inputDirection = forward * _input.move.y + right * _input.move.x;
            }

            _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            if (_animator != null)
            {
                float speedPercent = _speed / SprintSpeed;
                _animator.SetFloat("Running", speedPercent);
            }
        }

        private void Attack()
        {
            if (IsUiLocked() || Cursor.lockState != CursorLockMode.Locked || IsPointerOverUI())
            {
                _input.attack = false;
                return;
            }
            if (_animator == null) return;

            if (!InventoryManager.Instance.equippedItems.TryGetValue(ItemData.EquipmentSlot.Weapon, out ItemData weaponItem))
            {
                _input.attack = false;
                return;
            }
            if (weaponItem.equipSlot != ItemData.EquipmentSlot.Weapon)
            {
                Debug.Log("You need to equip a sword to attack.");
                _input.attack = false;
                return;
            }

            if (_input.attack && Time.time >= _lastAttackTime + attackCooldown && !staminaExhausted)
            {
                _animator.SetTrigger("Attack");
                if (attackSFX) _audioSource.PlayOneShot(attackSFX);
                Ray ray = new Ray(Camera.position, Camera.forward);
                Debug.DrawRay(ray.origin, ray.direction * attackDistance, Color.red, 10f);
                if (Physics.Raycast(ray, out RaycastHit hit, attackDistance))
                {
                    var npc = hit.collider.GetComponentInParent<NPCBehaviour>();
                    if (npc != null) npc.TakeDamage(attackDamage);

                    var enemy = hit.collider.GetComponent<EnemyBehavior>();
                    if (enemy != null) enemy.TakeDamage(attackDamage);

                    var guard = hit.collider.GetComponentInParent<GuardBehaviour>();
                    if (guard != null) guard.TakeDamage(attackDamage);

                    if (hitSFX != null) _audioSource.PlayOneShot(hitSFX);
                }
                PlayerStamina -= 10;
                if (PlayerStamina <= 10f)
                {
                    PlayerStamina = 0f;
                    staminaExhausted = true;
                    staminaCooldownTimer = StaminaCooldownDuration;
                }

                _lastAttackTime = Time.time;
                _input.attack = false;
            }
            else if (_input.attack)
            {
                _input.attack = false;
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    if (!staminaExhausted && PlayerStamina >= 10)
                    {
                        _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                        PlayerStamina -= 10;
                        if (PlayerStamina <= 10f)
                        {
                            PlayerStamina = 0f;
                            staminaExhausted = true;
                            staminaCooldownTimer = StaminaCooldownDuration;
                        }
                    }

                    _input.jump = false;
                }
                if (_jumpTimeoutDelta >= 0.0f)
                    _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                    _fallTimeoutDelta -= Time.deltaTime;

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
                _verticalVelocity += Gravity * Time.deltaTime;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        public void playerDamaged(int amount)
        {
            PlayerHealth -= amount / Mathf.Max(1, Defense);
            if (healthbar) healthbar.value = PlayerHealth;

            if (PlayerHealth <= 0)
            {
                if (!isRespawning && enableRespawn)
                {
                    StartCoroutine(RespawnRoutine());
                }
                else
                {
                    isAlive = false;
                }

                InventoryManager.Instance.ClearInventory();
                return;
            }
        }

        public void ApplyEquipStats(ItemData item)
        {
            attackDamage += item.damage;
            Defense += item.armorBonus;
        }

        public void RemoveEquipStats(ItemData item)
        {
            attackDamage -= item.damage;
            Defense -= item.armorBonus;
        }

        public void Heal(float amount)
        {
            if (!isAlive) return;

            PlayerHealth += amount;
            if (PlayerHealth > MaxHealth) PlayerHealth = MaxHealth;

            if (healthbar) healthbar.value = PlayerHealth;
        }

        private IEnumerator RespawnRoutine()
        {
            isRespawning = true;
            isAlive = false;

            _input.enabled = false;
            _controller.enabled = false;

            if (_animator) _animator.SetTrigger("Death");
            if (deathSFX) _audioSource.PlayOneShot(deathSFX);
            var immersive = GetComponent<FP_immersive>();
            if (immersive) immersive.FadeBlack(1f, deathFadeDuration, 0f, false);

            yield return StartCoroutine(WaitForDeathAnimOrTimeout(deathFadeDuration + 0.75f));

            Transform spawn = CheckpointManager.Instance
                ? CheckpointManager.Instance.GetClosestAllowedSpawn(transform.position)
                : null;

            if (spawn != null)
            {
                transform.position = spawn.position;
                transform.rotation = spawn.rotation;
            }
            else
            {
                transform.position = fallbackSpawnPos;
                transform.rotation = fallbackSpawnRot;
            }

            float targetHealth = Mathf.Max(1f, MaxHealth * respawnHealthPct);
            PlayerHealth = Mathf.Clamp(targetHealth, 1f, MaxHealth);
            if (healthbar) healthbar.value = PlayerHealth;

            _verticalVelocity = 0f;
            staminaExhausted = false;
            PlayerStamina = MaxStamina;
            if (staminaSlider) staminaSlider.value = PlayerStamina;

            _controller.enabled = true;
            _input.enabled = true;
            isAlive = true;

            if (_animator)
            {
                _animator.ResetTrigger("Death");
                _animator.SetTrigger("Respawn");
            }
            if (immersive) immersive.FadeBlack(0f, fadeInDuration, respawnHoldTime, false);

            isRespawning = false;
        }

        private IEnumerator WaitForDeathAnimOrTimeout(float maxWaitSeconds)
        {
            float t = 0f;
            while (t < maxWaitSeconds)
            {
                if (_animator)
                {
                    var st = _animator.GetCurrentAnimatorStateInfo(0);
                    if (st.IsTag(deathStateTag) && st.normalizedTime >= 1f)
                        yield break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void UpdateLookHints()
        {
            if (stealCrosshair) stealCrosshair.SetActive(false);
            if (collectCrosshair) collectCrosshair.SetActive(false);
            if (normalCrosshair) normalCrosshair.SetActive(true);
            if (IsUiLocked()) return;

            Ray ray = new Ray(Camera.position, Camera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                var pickup = hit.collider.GetComponent<ItemCollection>();
                if (pickup != null)
                {
                    if (pickup.isOwned && !pickup.wasStolen)
                    {
                        if (stealCrosshair) stealCrosshair.SetActive(true);
                        if (normalCrosshair) normalCrosshair.SetActive(false);
                    }
                    else
                    {
                        if (collectCrosshair) collectCrosshair.SetActive(true);
                        if (normalCrosshair) normalCrosshair.SetActive(false);
                    }
                    return;
                }
            }
        }

        private void ToggleMap()
        {
            bool opening = !isMapOpen;
            if (opening && openUISFX) _audioSource.PlayOneShot(openUISFX);
            if (opening)
            {
                DialogueService.End();
                ShopService.End();
            }

            isMapOpen = opening;
            if (MapUI) MapUI.SetActive(isMapOpen);

            Cursor.lockState = isMapOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isMapOpen;

            _input.cursorLocked = !isMapOpen;
        }

    } 
}
