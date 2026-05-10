using UnityEngine;
using System;
using AnimationState = Villagers.Jobs.AnimationState;

[Serializable]
public abstract class JobLogic
{
    protected string LogCategory => GetType().Name;
    protected void LogError(string msg)   => GameLog.LogError(LogCategory, msg, null);
    protected void LogWarning(string msg) => GameLog.LogWarning(LogCategory, msg, null);
    protected void LogEvent(string msg)   => GameLog.LogEvent(LogCategory, msg, null);
    protected void LogInfo(string msg)    => GameLog.LogInfo(LogCategory, msg, null);
    protected void LogVerbose(string msg) => GameLog.LogVerbose(LogCategory, msg, null);

    [Header("Animation")]
    [Tooltip("Animator int parameter that mirrors the AnimationState.")]
    public string animatorStateParameter = "AnimationState";

    [NonSerialized] protected AnimationState _currentState = AnimationState.Idle;
    [NonSerialized] private bool _initialized = false;
    
    protected float timeSinceLastAction = 0f;
    protected string currentStatus = "Looking for work.";

    public void OnJobStart(JobHandler handler)
    {
        if (_initialized)
        {
            LogWarning($"OnJobStart called again on {handler.name} - ignoring");
            return;
        }

        _initialized = true;
        timeSinceLastAction = 0f;
        OnInitialize(handler);
    }

    public bool Execute(JobHandler handler)
    {
        if (!_initialized)
        {
            OnJobStart(handler);
        }

        return ExecuteState(handler);
    }

    public virtual void OnJobEnd(JobHandler handler)
    {
        if (handler != null && handler.equipment != null)
            handler.equipment.HideAll();
    }

    protected abstract void OnInitialize(JobHandler handler);
    protected abstract bool ExecuteState(JobHandler handler);

    protected void ChangeState(AnimationState newState, JobHandler handler)
    {
        if (_currentState == newState)
            return;

        LogInfo($"{handler.name}: {_currentState} -> {newState}");

        bool goingIdle = newState == AnimationState.Idle && _currentState == AnimationState.FindingTarget;

        var oldState = _currentState;
        _currentState = newState;
        timeSinceLastAction = 0f;

        handler?.NotifyStateChanged(oldState, newState);

        if (goingIdle)
            handler.NotifyIdle();

        if (handler != null && handler.animator != null && !string.IsNullOrEmpty(animatorStateParameter))
        {
            handler.animator.SetInteger(animatorStateParameter, (int)_currentState);
        }

        if (handler != null && handler.equipment != null)
        {
            handler.equipment.UpdateVisuals(handler.currentJob, _currentState);
        }
    }

    public virtual void ResetState()
    {
        _initialized = false;
        _currentState = AnimationState.Idle;
        timeSinceLastAction = 0f;
    }

    public string GetCurrentStatus() => currentStatus;

    public AnimationState GetCurrentState() => _currentState;

    public JobLogic Clone() => (JobLogic)MemberwiseClone();
}