using System;
using UnityEngine;
using Tiles;

[CreateAssetMenu(menuName = "Game/Villager Audio Data")]
public class VillagerAudioData : ScriptableObject
{
    [Serializable]
    public class JobAudioMapping
    {
        public JobType jobType;

        [Tooltip("Clips played on a rhythmic interval during work states (Chopping, Mining, Building, etc.)")]
        public AudioClip[] workImpactClips;

        [Tooltip("Seconds to wait before the first impact sound (use to sync with the hit frame in the animation)")]
        public float workSoundDelay = 0f;

        [Tooltip("Seconds between impact sounds during work")]
        public float workImpactInterval = 1.2f;

        [Tooltip("Clip played when entering the Carrying state")]
        public AudioClip[] carryStartClips;
    }

    [Serializable]
    public class FootstepSurface
    {
        public TileStyle tileStyle;
        public AudioClip[] clips;

        [Tooltip("Seconds between footstep sounds for this surface")]
        public float stepInterval = 0.45f;
    }

    [Header("Job-Specific Sounds")]
    public JobAudioMapping[] jobMappings;

    [Header("Footsteps")]
    public FootstepSurface[] footstepSurfaces;

    [Header("Idle Sounds")]
    [Tooltip("Random ambient clips played while the villager is looking for work")]
    public AudioClip[] idleClips;

    [Tooltip("Minimum seconds between idle sounds")]
    public float idleIntervalMin = 8f;

    [Tooltip("Maximum seconds between idle sounds")]
    public float idleIntervalMax = 20f;

    public JobAudioMapping GetJobMapping(JobType jobType)
    {
        if (jobMappings == null || jobType == null) return null;
        foreach (var m in jobMappings)
            if (m.jobType == jobType) return m;
        return null;
    }

    public FootstepSurface GetFootstepSurface(TileStyle style)
    {
        if (footstepSurfaces != null)
        {
            foreach (var s in footstepSurfaces)
                if (s.tileStyle == style) return s;
            foreach (var s in footstepSurfaces)
                if (s.tileStyle == TileStyle.Grass) return s;
        }
        return null;
    }
}
