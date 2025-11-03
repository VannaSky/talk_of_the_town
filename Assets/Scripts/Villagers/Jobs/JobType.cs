using UnityEngine;

[CreateAssetMenu(fileName = "JobType", menuName = "Jobs/Job Type")]
public class JobType : ScriptableObject
{
    public string JobName;
    public Sprite JobIcon;

    [SerializeReference]
    public JobLogic JobLogic;
}