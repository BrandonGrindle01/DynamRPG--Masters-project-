using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FP_immersive : MonoBehaviour
{
    [Header("Head Bobbing")]
    public Transform cameraTransform;
    public float bobSpeed = 14f;
    public float bobAmount = 0.05f;

    [Header("Footsteps")]
    public AudioSource footstepSource;
    public AudioClip[] footstepClips;
    public float footstepDelay = 0.5f;

    private CharacterController controller;
    private float defaultY;
    private float footstepTimer;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform != null)
        {
            defaultY = cameraTransform.localPosition.y;
        }
    }

    void Update()
    {
        if (cameraTransform == null || controller == null) return;

        HandleHeadBob();
        HandleFootsteps();
    }

    void HandleHeadBob()
    {
        if (controller.velocity.magnitude > 0.1f && controller.isGrounded)
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                defaultY + bobOffset,
                cameraTransform.localPosition.z
            );
        }
        else
        {
            Vector3 pos = cameraTransform.localPosition;
            pos.y = Mathf.Lerp(pos.y, defaultY, Time.deltaTime * 5f);
            cameraTransform.localPosition = pos;
        }
    }

    void HandleFootsteps()
    {
        if (controller.isGrounded && controller.velocity.magnitude > 0.2f)
        {
            footstepTimer += Time.deltaTime;
            if (footstepTimer >= footstepDelay)
            {
                if (footstepClips.Length > 0)
                {
                    footstepSource.PlayOneShot(footstepClips[Random.Range(0, footstepClips.Length)]);
                }
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }
}
