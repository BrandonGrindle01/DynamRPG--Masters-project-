using UnityEngine;

[CreateAssetMenu(menuName = "Quests/Beacon Params", fileName = "QuestBeaconParams")]
public class QuestBeaconParamsAsset : ScriptableObject
{
    [Header("Visuals")]
    [Tooltip("A URP Unlit/Lit material set to Transparent. Will be cloned at runtime.")]
    public Material materialTemplate;

    [Tooltip("Optional icon to place on the beacon quad (sets _BaseMap/_MainTex if present).")]
    public Texture2D iconTexture;

    [Tooltip("Color when pointing to the quest giver (checkmark).")]
    public Color normalColor = new Color(0.2f, 0.95f, 1f, 0.9f);

    [Tooltip("Color when we want a '?' style prompt (e.g., pending/turn-in).")]
    public Color questionColor = new Color(1f, 0.95f, 0.2f, 0.95f);

    [Header("Placement (for quad beacons)")]
    [Min(0f)] public float verticalOffset = 2.2f;
    [Min(0.1f)] public float scale = 1.2f;

    [Tooltip("If true, the quad beacon will face the camera.")]
    public bool faceCamera = true;

    [Header("Fallbacks (used if materialTemplate is null)")]
    [Tooltip("Will try these in order until a shader is found.")]
    public string[] fallbackShaders = new[]
    {
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Lit",
        "Sprites/Default",
        "UI/Default",
        "Standard"
    };
}