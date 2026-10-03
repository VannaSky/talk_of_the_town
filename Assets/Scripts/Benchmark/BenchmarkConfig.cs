using System;
using System.Collections.Generic;
using Tiles;
using UnityEngine;

namespace Benchmark
{
    public enum RunStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    /// <summary>
    /// A model with its per-model think mode setting.
    /// Non-reasoning models (llama, gemma, qwen2.5) should use ModelDefault.
    /// gpt-oss supports levels (Low/Medium/High); on/off thinking models (qwen) use On or Off.
    /// </summary>
    [Serializable]
    public class ModelConfig
    {
        public string modelName;
        public ThinkMode thinkMode = ThinkMode.ModelDefault;
        public PromptStyle promptStyle = PromptStyle.Normal;
        public bool forceJsonFormat = false;
        [Tooltip("0 = Ollama default. Reasoning models need 2048+ to leave room for thinking + output.")]
        public int maxOutputTokens = 0;
        [Tooltip("0 = model default context window. Set higher for models with small defaults (e.g. gemma3 = 8K).")]
        public int contextSize = 0;
    }

    /// <summary>
    /// A named set of goals that can be configured in the Inspector.
    /// E.g. "1 Goal" = just Wood 100, "3 Goals" = Wood 100 + Stone 80 + Food 60.
    /// </summary>
    [Serializable]
    public class GoalPreset
    {
        public string label;                      // e.g. "1goal", "3goals", "5goals"
        public List<GoalConfig> goals = new();
    }

    [Serializable]
    public class GoalConfig
    {
        public GlobalGoalType type;
        public string targetResource; // ResourceType name, e.g. "Wood"
        public int targetAmount;

        public GlobalGoal ToGlobalGoal()
        {
            var goal = new GlobalGoal
            {
                type = type,
                targetAmount = targetAmount,
                isCompleted = false,
                completionTime = 0f
            };

            if (type == GlobalGoalType.ResourceAmount)
            {
                if (Enum.TryParse<ResourceType>(targetResource, true, out var rt))
                    goal.targetResource = rt;
            }

            return goal;
        }
    }

    [Serializable]
    public class BenchmarkRunConfig
    {
        public string runId;
        public string modelName;
        public string thinkMode;     // ThinkMode to apply for this model
        public string promptStyle;   // PromptStyle to apply for this run
        public bool forceJsonFormat; // Force JSON structured output via Ollama
        public int maxOutputTokens;  // num_predict for this model
        public int contextSize;      // num_ctx override (0 = model default)
        public string mapFile;       // .twcmap filename
        public string mapSize;       // "small" or "large" (metadata label)
        public List<GoalConfig> goals = new();
        public int repetition;       // 1 or 2
        public long cutoffTicks;     // max sim-ticks before forced abort
        public float gameSpeed;      // Time.timeScale during the run
        public RunStatus status = RunStatus.Pending;
        public string completedAt;   // ISO 8601 timestamp
        public string abortReason;   // "goals_reached", "cutoff", "error:...", "skipped"
    }

    [Serializable]
    public class BenchmarkManifest
    {
        public string createdAt;
        public string configHash;    // hash of the Inspector config the manifest was generated from
        public List<BenchmarkRunConfig> runs = new();
    }

    // ── Lightweight snapshot of dynamic state at time of LLM call ────────

    [Serializable]
    public class InputStateSnapshot
    {
        public int wood;
        public int stone;
        public int seeds;
        public int food;
        public int capacity;
        public int villagerCount;
        public List<string> idleVillagers = new();
        public List<string> activeGoals = new();
        public int buildingCount;
        public List<string> completedBuildings = new();
        public List<string> unfinishedBuildings = new();
        public List<string> freeBuildSites = new();
    }

    [Serializable]
    public class ParsedAssignment
    {
        public string villager;
        public string job;
        public string buildingType;
        public int targetX;
        public int targetY;
        public int gatherAmount;
        public int restUntilEnergy;
        public string reason;
    }

    [Serializable]
    public class ParsedGoal
    {
        public string type;
        public string resource;
        public int amount;
        public string priority;
        public string description;
    }

    [Serializable]
    public class TokenCount
    {
        public int prompt;
        // Ollama quirk: local models with format=json leave thinking tokens out of eval_count, so this
        // undercounts reasoning models (e.g. qwen3.5). Compare reasoning via responseTimeSeconds and thinkingChars.
        public int response;
        public int total;
    }

    [Serializable]
    public class LLMDecisionLogEntry
    {
        public long simTick;             // tick when the answer arrived
        public long requestStartTick;    // tick when the call was sent; equal to simTick when the game paused correctly
        public string triggerReason;
        public string contextType; // "full" or "delta"
        public InputStateSnapshot inputState;
        public string rawResponse;
        public string thinking;          // separate reasoning trace from Ollama (empty if none)
        public int thinkingChars;        // length of thinking (token counts can miss it, see TokenCount)
        public string doneReason;        // "stop", or "length" if truncated
        public string actualModel;       // model name reported by Ollama
        public string systemPromptFile;  // file in the run folder holding the system prompt used for this call
        public string userPrompt;        // full context sent with this call
        public List<ParsedAssignment> parsedAssignments = new();
        public List<ParsedGoal> parsedGoals = new();
        public TokenCount tokenCount;
        public double responseTimeSeconds;
        public bool success;
        public string errorMessage;
    }

    // ── Data passed via the new LLMController event ──────────────────────

    public class BatchDecisionLog
    {
        public long simTick;
        public long requestStartTick;
        public string triggerReason;
        public string contextType; // "full" or "delta"
        public InputStateSnapshot inputState;
        public string rawResponse;
        public string systemPrompt;
        public string userPrompt;
        public Dictionary<string, JobDecision> parsedDecisions;
        public List<RawGoalDecision> parsedGoals;
        public LLMMetrics metrics;
    }

    // ── World event entry ────────────────────────────────────────────────

    [Serializable]
    public class WorldEventEntry
    {
        public long simTick;
        public string eventType;
        public string details; // JSON sub-object as string for flexibility
    }

    // ── Run metadata ─────────────────────────────────────────────────────

    [Serializable]
    public class MapStatistics
    {
        public int totalTiles;
        public int grassTiles;
        public int forestTiles;
        public int mountainTiles;
        public int waterTiles;
        public int coastTiles;
        public int treeCount;
        public int stoneCount;
        public int seedCount;
        public int mineCount;
        public int cropCount;
    }

    [Serializable]
    public class LLMSettings
    {
        public bool useConversationMemory;
        public int memoryPairs;
        public string thinkMode;
        public int contextSize;
        public string promptStyle;
        public float decisionDebounceSeconds;  // game seconds collect window before a call
        public float fallbackIntervalSeconds;  // game seconds before a fallback call
    }

    /// <summary>Model as reported by Ollama at run start. Cloud models can change behind the same name — the digest shows it.</summary>
    [Serializable]
    public class ModelInfo
    {
        public string ollamaVersion;
        public bool foundInModelList;
        public string name;
        public string digest;
        public string modifiedAt;
        public long sizeBytes;
        public string family;
        public string parameterSize;
        public string quantizationLevel;
    }

    /// <summary>Where and how fast the run was simulated, to show that all runs had the same conditions.</summary>
    [Serializable]
    public class RunEnvironment
    {
        public bool isEditor;
        public string platform;
        public string machineName;
        public string operatingSystem;
        public string processorType;
        public int processorCount;
        public int systemMemoryMB;
        public string graphicsDevice;
        public float maximumDeltaTime;
        public float avgFps;                 // frames / real seconds over the whole run (incl. LLM pauses)
        public float llmWaitSeconds;         // real seconds spent waiting for LLM answers
        public float llmWaitShare;           // llmWaitSeconds / elapsed real time
        public float simSpeedWhileRunning;   // game seconds / real seconds the game was NOT waiting for the LLM
    }

    [Serializable]
    public class RunMetadata
    {
        public string runId;
        public string modelName;
        public string actualModel;       // model the LLMController actually used
        public string thinkMode;
        public string promptStyle;
        public bool forceJsonFormat;
        public int maxOutputTokens;
        public int contextSize;
        public float gameSpeed;
        public string unityVersion;
        public string gitCommit;
        public string mapFile;
        public string mapSize;
        public int mapSeed;
        public List<GoalConfig> goals = new();
        public int repetition;
        public long cutoffTicks;
        public string startTime;
        public string endTime;
        public string abortReason;
        public long finalTick;
        public float elapsedGameTimeSeconds; // finalTick * TickQuantum (game time since run start)
        public float elapsedRealTimeSeconds;
        public MapStatistics mapStats;
        public LLMSettings llmSettings;
        public LLMSessionStats sessionStats;
        public ModelInfo modelInfo;
        public RunEnvironment environment;
    }
}
