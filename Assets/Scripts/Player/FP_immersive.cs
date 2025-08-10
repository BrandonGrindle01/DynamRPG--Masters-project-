using StarterAssets;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

    [Header("UI")]
    public Image fade;
    [Range(0.1f, 10f)] public float fadeSpeed = 3f;

    private CharacterController controller;
    private float defaultY;
    private float footstepTimer;
    private StarterAssetsInputs input;
    private Coroutine fadeRoutine;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<StarterAssetsInputs>();

        if (cameraTransform != null)
        {
            defaultY = cameraTransform.localPosition.y;
        }

        fade.gameObject.SetActive(false);
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

    public void FadeBlack(float toAlpha, float duration, float hold = 0f, bool thenFadeBack = false)
    {
        if (!fade) return;
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(toAlpha, duration, hold, thenFadeBack));
    }

    private IEnumerator FadeRoutine(float toAlpha, float duration, float hold, bool thenFadeBack)
    {
        var c = fade.color;
        float from = c.a;
        float t = 0f;

        fade.gameObject.SetActive(true);

        fade.raycastTarget = true;

        duration = Mathf.Max(0.01f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, toAlpha, t / duration);
            fade.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        fade.color = new Color(c.r, c.g, c.b, toAlpha);

        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

        if (thenFadeBack)
        {
            float backT = 0f;
            while (backT < duration)
            {
                backT += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(toAlpha, 0f, backT / duration);
                fade.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            fade.color = new Color(c.r, c.g, c.b, 0f);
            fade.raycastTarget = false;
        }
        else
        {
            if (toAlpha <= 0.001f) fade.raycastTarget = false;
        }

        fade.gameObject.SetActive(false);
    }
}
