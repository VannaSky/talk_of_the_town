using UnityEngine;

[CreateAssetMenu(menuName = "Game/Ambient Audio Data")]
public class AmbientAudioData : ScriptableObject
{
    [Header("Ambience (looped, layered)")]
    [Tooltip("All clips play simultaneously. e.g. Ambiance_Forest_Birds, Ambiance_Wind_Calm")]
    public AudioClip[] ambientLayers;
    [Range(0f, 1f)] public float ambientVolume = 0.4f;

    [Header("Event Sounds (2D, non-spatial)")]
    [Tooltip("Played when any resource is gained. e.g. SFX_CollectItemDigitalv1")]
    public AudioClip[] resourceGainedClips;
    [Tooltip("Minimum real seconds between resource-gain sounds to avoid spam")]
    public float resourceSoundCooldown = 2f;

    [Tooltip("Played when a village goal is completed")]
    public AudioClip goalCompletedClip;

    [Tooltip("Played when a village goal is failed")]
    public AudioClip goalFailedClip;
}
