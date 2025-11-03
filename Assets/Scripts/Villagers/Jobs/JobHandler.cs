using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEditor;

public class JobHandler : MonoBehaviour
{
    [Serializable]
    public class JobLevelData
    {
        public JobType JobType;
        public int level = 0;
        public float experience = 0f;
    }

    [Header("Job State")]
    public JobType currentJob = null;

    private Dictionary<JobType, JobLevelData> jobLevels = new();

    [SerializeField]
    private List<JobLevelData> allJobLevels = new();

    private void Awake()
    {
        foreach (var jobData in allJobLevels)
        {
            jobLevels[jobData.JobType] = jobData;
        }
    }

    public void AssignJob(JobType newJob)
    {
        currentJob = newJob;

        if (!jobLevels.ContainsKey(newJob))
        {
            JobLevelData newLevelData = new()
            {
                JobType = newJob,
                level = 0,
                experience = 0f
            };

            jobLevels[newJob] = newLevelData;
            allJobLevels.Add(newLevelData);
        }
    }

    public int GetCurrentJobLevel()
    {
        if (currentJob != null && jobLevels.ContainsKey(currentJob))
        {
            return jobLevels[currentJob].level;
        }
        return 0;
    }

    public void JobFinished()
    {
        if (currentJob == null) return;

        if (jobLevels.TryGetValue(currentJob, out JobLevelData levelData))
        {
            levelData.experience += 10f;

            if (levelData.experience >= 100f)
            {
                levelData.level += 1;
                levelData.experience = 0f;
            }
        }
    }

    void Update()
    {
        if (currentJob == null) return;

        bool isJobComplete = currentJob.JobLogic.Execute(this);

        if (isJobComplete)
        {
            JobFinished();
        }
    }
}

[CustomEditor(typeof(JobHandler))]
public class JobHandlerEditor : Editor
{
    private JobType[] allJobTypes;

    private void OnEnable()
    {
        allJobTypes = Resources.LoadAll<JobType>("");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        JobHandler jobHandler = (JobHandler)target;

        GUILayout.Space(10);
        GUILayout.Label("Quick Job Assignment", EditorStyles.boldLabel);

        foreach (var jobType in allJobTypes)
        {
            if (GUILayout.Button(jobType.JobName))
            {
                jobHandler.AssignJob(jobType);

                EditorUtility.SetDirty(jobHandler);
            }
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Clear Job"))
        {
            jobHandler.AssignJob(null);
            EditorUtility.SetDirty(jobHandler);
        }
    }
}