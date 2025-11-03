using System;

[Serializable]
public abstract class JobLogic
{
    protected float timeSinceLastAction = 0f;
    protected string currentStatus = "Looking for work.";

    public abstract bool Execute(JobHandler jobHandler);
    public virtual void OnJobStart(JobHandler jobHandler) { }
    public virtual void OnJobEnd(JobHandler jobHandler) { }
    public abstract string GetCurrentStatus();
}