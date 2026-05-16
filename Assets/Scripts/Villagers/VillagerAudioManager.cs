using System.Collections;
using Tiles;
using UnityEngine;
using AnimationState = Villagers.Jobs.AnimationState;

[RequireComponent(typeof(JobHandler))]
[RequireComponent(typeof(Villager))]
public class VillagerAudioManager : MonoBehaviour
{
    [Header("Audio Data")]
    public VillagerAudioData audioData;

    [Header("Audio Sources")]
    [Tooltip("Plays footsteps, carry sounds, and idle chatter. Auto-created if left empty.")]
    [SerializeField] private AudioSource voiceSource;

    [Tooltip("Plays rhythmic work impact sounds. Auto-created if left empty.")]
    [SerializeField] private AudioSource workSource;

    private JobHandler _jobHandler;
    private Villager _villager;
    private VillagerMover _mover;

    private Coroutine _footstepCoroutine;
    private Coroutine _idleCoroutine;
    private Coroutine _workCoroutine;

    void Awake()
    {
        _jobHandler = GetComponent<JobHandler>();
        _villager = GetComponent<Villager>();
        _mover = GetComponent<VillagerMover>();

        if (voiceSource == null)
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.spatialBlend = 1f;
            voiceSource.rolloffMode = AudioRolloffMode.Linear;
            voiceSource.maxDistance = 20f;
            voiceSource.playOnAwake = false;
        }

        if (workSource == null)
        {
            workSource = gameObject.AddComponent<AudioSource>();
            workSource.spatialBlend = 1f;
            workSource.rolloffMode = AudioRolloffMode.Linear;
            workSource.maxDistance = 15f;
            workSource.playOnAwake = false;
        }
    }

    void OnEnable()
    {
        _jobHandler.OnAnimationStateChanged += HandleStateChanged;
        _jobHandler.OnBecameIdle += HandleBecameIdle;
        _jobHandler.OnJobAssigned += HandleJobAssigned;
    }

    void OnDisable()
    {
        _jobHandler.OnAnimationStateChanged -= HandleStateChanged;
        _jobHandler.OnBecameIdle -= HandleBecameIdle;
        _jobHandler.OnJobAssigned -= HandleJobAssigned;
    }

    void Start()
    {
        if (audioData == null) return;
        _idleCoroutine = StartCoroutine(IdleLoop());
    }

    private void HandleStateChanged(AnimationState oldState, AnimationState newState)
    {
        if (audioData == null) return;

        if (newState == AnimationState.MovingToTarget)
            StartFootsteps();
        else
            StopFootsteps();

        if (IsWorkState(newState))
            StartWorkSounds();
        else
            StopWorkSounds();

        if (newState == AnimationState.Carrying && oldState != AnimationState.Carrying)
            PlayOneShot(voiceSource, GetCarryClips());
    }

    private void HandleBecameIdle(JobHandler handler)
    {
        StopFootsteps();
        StopWorkSounds();
    }

    private void HandleJobAssigned(JobType job)
    {
        // Stop all active sounds immediately on any job change (including going idle)
        StopFootsteps();
        StopWorkSounds();
    }

    // --- Footsteps ---

    private void StartFootsteps()
    {
        StopFootsteps();
        _footstepCoroutine = StartCoroutine(FootstepLoop());
    }

    private void StopFootsteps()
    {
        if (_footstepCoroutine == null) return;
        StopCoroutine(_footstepCoroutine);
        _footstepCoroutine = null;
    }

    private IEnumerator FootstepLoop()
    {
        while (true)
        {
            if (_mover != null && _mover.IsMoving())
            {
                var surface = GetCurrentSurface();
                if (surface?.clips?.Length > 0)
                    PlayOneShot(voiceSource, surface.clips);
                yield return new WaitForSeconds(surface?.stepInterval ?? 0.45f);
            }
            else
            {
                yield return null;
            }
        }
    }

    private VillagerAudioData.FootstepSurface GetCurrentSurface()
    {
        var style = _villager?.CurrentTile?.Archetype?.Style ?? TileStyle.Grass;
        return audioData.GetFootstepSurface(style);
    }

    // --- Work Sounds ---

    private void StartWorkSounds()
    {
        StopWorkSounds();
        var mapping = audioData.GetJobMapping(_jobHandler.currentJob);
        if (mapping?.workImpactClips?.Length > 0)
            _workCoroutine = StartCoroutine(WorkSoundLoop(mapping));
    }

    private void StopWorkSounds()
    {
        if (_workCoroutine != null)
        {
            StopCoroutine(_workCoroutine);
            _workCoroutine = null;
        }
        workSource.Stop();
    }

    private IEnumerator WorkSoundLoop(VillagerAudioData.JobAudioMapping mapping)
    {
        if (mapping.workSoundDelay > 0f)
            yield return new WaitForSeconds(mapping.workSoundDelay);

        while (true)
        {
            PlayOneShot(workSource, mapping.workImpactClips);
            yield return new WaitForSeconds(mapping.workImpactInterval);
        }
    }

    // --- Idle Sounds ---

    private IEnumerator IdleLoop()
    {
        while (true)
        {
            float wait = Random.Range(audioData.idleIntervalMin, audioData.idleIntervalMax);
            yield return new WaitForSeconds(wait);
            if (audioData.idleClips?.Length > 0 && !voiceSource.isPlaying)
                PlayOneShot(voiceSource, audioData.idleClips);
        }
    }

    // --- Helpers ---

    private static bool IsWorkState(AnimationState state) =>
        state == AnimationState.Chopping  ||
        state == AnimationState.Mining    ||
        state == AnimationState.Gathering ||
        state == AnimationState.Building  ||
        state == AnimationState.Farming   ||
        state == AnimationState.Planting;

    private AudioClip[] GetCarryClips() =>
        audioData.GetJobMapping(_jobHandler.currentJob)?.carryStartClips;

    private void PlayOneShot(AudioSource source, AudioClip[] clips)
    {
        if (source == null || clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) source.PlayOneShot(clip);
    }
}
