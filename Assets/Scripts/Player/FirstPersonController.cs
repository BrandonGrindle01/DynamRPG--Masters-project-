using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;

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
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		
		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
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



		// Custom additions
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
        //[SerializeField] private float attackSpeed = 1.0f;
        public int attackDamage = 1;

        [SerializeField] private GameObject Hitfx;
        [SerializeField] private AudioClip swingSFX;
        [SerializeField] private AudioClip HitSFX;
        int attackcount;

        private float _lastAttackTime = -999f;

        [Header("Inventory UI")]
        [SerializeField] private GameObject inventoryUI;
        public bool isInventoryOpen = false;


		[Header("equipment manager")]
        private Dictionary<ItemData.EquipmentSlot, ItemData> equippedItems = new();
        public Transform weaponHolder;
        public Transform[] armorHolder = new Transform[4];


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
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
            instance = this;
        }

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

			healthbar = GameObject.Find("HealthBar")?.GetComponent<Slider>();
            healthbar.maxValue = PlayerHealth; 
            healthbar.value = PlayerHealth; 
            
            staminaSlider = GameObject.Find("Sprint")?.GetComponent <Slider>();
            staminaSlider.maxValue = MaxStamina;
            staminaSlider.value = PlayerStamina;

            _animator = GetComponentInChildren<Animator>();
			_audioSource = GetComponent<AudioSource>();
            _jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			JumpAndGravity();
			GroundedCheck();
			Move();
			Attack();
            UpdateUI();
            if (_input.openInventory)
            {
                ToggleInventory();
				Debug.Log("!toggling inventory");
                _input.openInventory = false;
            }

            if (_input.interact)
            {
                TryPickupItem();
                _input.interact = false;
            }

            if (PlayerHealth > MaxHealth)
                PlayerHealth = MaxHealth;

            healthbar.maxValue = MaxHealth;
        }

        private void UpdateUI()
        {
            healthbar.value = PlayerHealth;
            staminaSlider.value = PlayerStamina;
        }

        private void LateUpdate()
		{
			CameraMovement();
		}

        private void ToggleInventory()
        {
            isInventoryOpen = !isInventoryOpen;
            inventoryUI.SetActive(isInventoryOpen);
            _input.cursorLocked = !isInventoryOpen;
            Cursor.lockState = isInventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isInventoryOpen;
        }

        private void TryPickupItem()
        {
            Ray ray = new Ray(Camera.position, Camera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3f))
            {
                var pickup = hit.collider.GetComponent<ItemCollection>();
                if (pickup != null)
                {
                    if (pickup.isOwned && !pickup.wasStolen)
                    {
                        pickup.MarkAsStolen();
                        // Optional: notify crime system here
                    }
                    InventoryManager.Instance.AddItem(pickup.itemData, pickup.amount);
                    Destroy(hit.collider.gameObject);
                }
            }
        }

        private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraMovement()
		{
			if(!_animator) return;

			var Mouse_X = _input.look.x;
			var Mouse_Y = _input.look.y;
			Camera.position = CameraRoot.position;

			_xRot += Mouse_Y * MouseSensitivity * Time.deltaTime;
			_xRot = Mathf.Clamp(_xRot, UpperLimit, LowerLimit);

			Camera.localRotation = Quaternion.Euler(_xRot, 0, 0);
			transform.Rotate(Vector3.up, Mouse_X * MouseSensitivity * Time.deltaTime);
		}

		private void Move()
		{

            // handle stamina cooldown timer
            if (staminaExhausted)
            {
                staminaCooldownTimer -= Time.deltaTime;
                if (staminaCooldownTimer <= 0f && PlayerStamina > 10f) 
                {
                    staminaExhausted = false;
                }
            }

            // Handle stamina logic before speed calc
            bool wantsToSprint = _input.sprint && !staminaExhausted && PlayerStamina > 0 && _input.move != Vector2.zero;
            isSprinting = wantsToSprint;

            // Sprint is only allowed if stamina > 0
            float targetSpeed = isSprinting ? SprintSpeed : MoveSpeed;
            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon
            // custom stamina logic.
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
                if (PlayerStamina > MaxStamina)
                    PlayerStamina = MaxStamina;
            }

            staminaSlider.value = PlayerStamina; 
            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			// move the player
			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
            if (_animator != null)
            {
                float speedPercent = _speed / SprintSpeed;
                _animator.SetFloat("Running", speedPercent);
            }
        }

        private void Attack()
        {
            if (_animator == null) return;

            if (!equippedItems.TryGetValue(ItemData.EquipmentSlot.Weapon, out ItemData weaponItem))
            {
                _input.attack = false;
                return;
            }
            if (weaponItem.itemName != "Sword")
            {
                Debug.Log("You need to equip a sword to attack.");
                _input.attack = false;
                return;
            }

            if (_input.attack && Time.time >= _lastAttackTime + attackCooldown)
            {
                Debug.Log("attacking");
                _animator.SetTrigger("Attack");

                Ray ray = new Ray(Camera.position, Camera.forward);

                if (Physics.Raycast(ray, out RaycastHit hit, attackDistance))
                {
                    Debug.Log("Hit object: " + hit.collider.name);

                    EnemyBehavior enemy = hit.collider.GetComponent<EnemyBehavior>();
                    if (enemy != null)
                    {
                        enemy.TakeDamage(attackDamage);

                        if (Hitfx != null)
                            Instantiate(Hitfx, hit.point, Quaternion.identity);

                        if (HitSFX != null)
                            _audioSource.PlayOneShot(HitSFX);
                    }
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
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

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

		public void playerDamaged(int amount)
		{
            PlayerHealth -= amount / Defense;
            healthbar.value = PlayerHealth;
            //if (painGrunt.Length > 0)
            //{
            //    int index = Random.Range(0, painGrunt.Length);
            //    AudioSource.PlayClipAtPoint(painGrunt[index], _controller.center);
            //}

            if (PlayerHealth <= 0)
            {
                isAlive = false;
                //_animator.SetBool(_animDeath, true);
                //if (death.Length > 0)
                //{
                //    int index = Random.Range(0, death.Length);
                //    AudioSource.PlayClipAtPoint(death[index], _controller.center, 1.8f);
                //}
               InventoryManager.Instance.ClearInventory();
                //StartCoroutine(DelayAD(8));

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

            // Clamp to max value if you have a max health (optional)
            if (PlayerHealth > healthbar.maxValue)
                PlayerHealth = healthbar.maxValue;

            // Update the UI
            healthbar.value = PlayerHealth;

            // Optionally, you can add visual/audio feedback here
            Debug.Log("Healed for " + amount + ", current health: " + PlayerHealth);
        }

        //private void OnDrawGizmosSelected()
        //{
        //	Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
        //	Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

        //	if (Grounded) Gizmos.color = transparentGreen;
        //	else Gizmos.color = transparentRed;

        //	// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
        //	Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        //}
    }
}