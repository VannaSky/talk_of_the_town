using UnityEngine;

[CreateAssetMenu(fileName = "JobType", menuName = "Game/Job Type")]
public class JobType : ScriptableObject
{
    public string JobName;
    public Sprite JobIcon;

    [SerializeReference, SubclassSelector]
    public JobLogic JobLogic;
}