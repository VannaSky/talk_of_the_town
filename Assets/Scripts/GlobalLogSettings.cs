using UnityEngine;

public class GlobalSettings : MonoBehaviour
{
    public static GlobalSettings Instance { get; private set; }

    [SerializeField] private LogLevel logLevel = LogLevel.Warning;

    public LogLevel LogLevel
    {
        get => logLevel;
        set { logLevel = value; GameLog.GlobalLevel = value; }
    }
    [SerializeField] private string llmModel = "";
    [SerializeField] private PromptStyle promptStyle = PromptStyle.Normal;

    public string LLMModel
    {
        get => llmModel;
        set => llmModel = value;
    }

    public PromptStyle PromptStyle
    {
        get => promptStyle;
        set => promptStyle = value;
    }

    [SerializeField] private PromptHints promptHints = PromptHints.Advisory;

    public PromptHints PromptHints
    {
        get => promptHints;
        set => promptHints = value;
    }

    /// <summary>True when the state context should state facts only, without build advice (see <see cref="global::PromptHints"/>).</summary>
    public static bool FactualHints => Instance != null && Instance.promptHints == PromptHints.Factual;

    /// <summary>Kept for backward compatibility with UI toggle. Maps Caveman ↔ Normal.</summary>
    public bool UseCavemanPrompt
    {
        get => promptStyle == PromptStyle.Caveman;
        set => promptStyle = value ? PromptStyle.Caveman : PromptStyle.Normal;
    }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        GameLog.GlobalLevel = logLevel;
    }

    private void OnValidate()
    {
        GameLog.GlobalLevel = logLevel;
    }
}

public enum PromptStyle
{
    Normal,
    Lean,
    Caveman,
    NormalOld,
    CavemanOld
}

/// <summary>
/// How the per-call state context (user prompt) phrases resource / population / storage tags.
/// Advisory = the original wording used by the 2026-10 benchmark matrix — keep it the default, it must not change.
/// Factual  = the same facts without goal-unaware build advice ("No free slots — build a House", "build Stockpile"
///            on FULL storage, "Focus on … village growth"). Ablation for the gemma3 build spiral (2026-10-04).
/// The system prompt (rules) is identical in both.
/// </summary>
public enum PromptHints
{
    Advisory,
    Factual
}