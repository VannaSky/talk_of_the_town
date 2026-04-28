using Tiles;
using UnityEngine;

/// <summary>
/// Scene-level singleton that manages layered ambient audio loops
/// and reacts to village events (resource collected, goal completed/failed).
/// Add to a persistent GameObject in the scene and assign an AmbientAudioData asset.
/// </summary>
public class AmbientAudioController : MonoBehaviour
{
    public static AmbientAudioController Instance { get; private set; }

    [Header("Audio Data")]
    public AmbientAudioData audioData;

    private AudioSource[] _ambientSources;
    private AudioSource _stingerSource;

    private float _lastResourceSoundTime = -999f;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        SetupAudioSources();
    }

    void Start()
    {
        SubscribeToEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    // -------------------------------------------------------------------------
    // Audio Source Setup
    // -------------------------------------------------------------------------

    private void SetupAudioSources()
    {
        if (audioData == null) return;

        _ambientSources = CreateLayerSources(audioData.ambientLayers, audioData.ambientVolume);

        _stingerSource = gameObject.AddComponent<AudioSource>();
        _stingerSource.playOnAwake = false;
        _stingerSource.spatialBlend = 0f;
        _stingerSource.volume = 0.8f;
    }

    private AudioSource[] CreateLayerSources(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return new AudioSource[0];

        var sources = new AudioSource[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clips[i];
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = volume;
            src.playOnAwake = false;
            if (clips[i] != null)
                src.Play();
            sources[i] = src;
        }
        return sources;
    }

    // -------------------------------------------------------------------------
    // Event Subscriptions
    // -------------------------------------------------------------------------

    private void SubscribeToEvents()
    {
        if (VillageState.Instance != null)
            VillageState.Instance.OnResourceChanged += OnResourceChanged;

        if (VillageGoals.Instance != null)
        {
            VillageGoals.Instance.OnGoalCompleted += OnGoalCompleted;
            VillageGoals.Instance.OnGoalFailed    += OnGoalFailed;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (VillageState.Instance != null)
            VillageState.Instance.OnResourceChanged -= OnResourceChanged;

        if (VillageGoals.Instance != null)
        {
            VillageGoals.Instance.OnGoalCompleted -= OnGoalCompleted;
            VillageGoals.Instance.OnGoalFailed    -= OnGoalFailed;
        }
    }

    private void OnResourceChanged(ResourceType type, int oldValue, int newValue)
    {
        if (audioData == null) return;
        if (newValue <= oldValue) return;  // only react to gains
        if (Time.time - _lastResourceSoundTime < audioData.resourceSoundCooldown) return;

        _lastResourceSoundTime = Time.time;
        PlayStinger(audioData.resourceGainedClips);
    }

    private void OnGoalCompleted(VillageGoal goal)
    {
        if (audioData?.goalCompletedClip != null)
            _stingerSource.PlayOneShot(audioData.goalCompletedClip);
    }

    private void OnGoalFailed(VillageGoal goal)
    {
        if (audioData?.goalFailedClip != null)
            _stingerSource.PlayOneShot(audioData.goalFailedClip);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void PlayStinger(AudioClip[] clips)
    {
        if (_stingerSource == null || clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
            _stingerSource.PlayOneShot(clip);
    }
}
