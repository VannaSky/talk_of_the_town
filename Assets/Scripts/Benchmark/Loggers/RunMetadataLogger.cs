using System;
using System.IO;
using System.Linq;
using Environment.Resources;
using Tiles;
using UnityEngine;

namespace Benchmark.Loggers
{
    /// <summary>
    /// Captures run metadata at start and writes run_metadata.json at finalization.
    /// </summary>
    public class RunMetadataLogger
    {
        private readonly string _outputDir;
        private readonly RunMetadata _metadata;
        private readonly float _startRealTime;
        private readonly int _startFrame;

        public RunMetadataLogger(string outputDir, BenchmarkRunConfig config)
        {
            _outputDir = outputDir;
            _startRealTime = Time.realtimeSinceStartup;
            _startFrame = Time.frameCount;

            _metadata = new RunMetadata
            {
                runId = config.runId,
                modelName = config.modelName,
                thinkMode = config.thinkMode,
                promptStyle = config.promptStyle,
                forceJsonFormat = config.forceJsonFormat,
                maxOutputTokens = config.maxOutputTokens,
                contextSize = config.contextSize,
                gameSpeed = config.gameSpeed,
                unityVersion = Application.unityVersion,
                gitCommit = ReadGitCommit(),
                mapFile = config.mapFile,
                mapSize = config.mapSize,
                goals = config.goals,
                repetition = config.repetition,
                cutoffTicks = config.cutoffTicks,
                startTime = DateTime.Now.ToString("o")
            };

            CaptureModelInfo(config.modelName);
        }

        /// <summary>
        /// Asks Ollama for the model digest and server version. Runs in the background; the run start does not
        /// wait for it, the result only has to be there when the metadata is written at the end.
        /// </summary>
        private async void CaptureModelInfo(string modelName)
        {
            var info = new ModelInfo { name = modelName };
            _metadata.modelInfo = info;
            try
            {
                using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    string json = await http.GetStringAsync("http://localhost:11434/api/version");
                    info.ollamaVersion = JsonUtility.FromJson<OllamaVersion>(json)?.version;
                }

                var models = await ollama.Ollama.List();
                var m = models?.FirstOrDefault(x => x.name == modelName)
                        ?? models?.FirstOrDefault(x => x.name == modelName + ":latest");
                if (m == null) return;

                info.foundInModelList = true;
                info.digest = m.digest;
                info.modifiedAt = m.modified_at.ToString("o");
                info.sizeBytes = m.size;
                info.family = m.details?.family;
                info.parameterSize = m.details?.parameter_size;
                info.quantizationLevel = m.details?.quantization_level;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunMetadataLogger] Could not read model info for {modelName}: {e.Message}");
            }
        }

        [Serializable]
        private class OllamaVersion { public string version; }

        /// <summary>
        /// Scans the loaded map to collect tile and resource statistics.
        /// Call after the map is fully loaded and resources are spawned.
        /// </summary>
        public void CaptureMapStatistics()
        {
            var stats = new MapStatistics();

            // Count tiles by type
            var tileGrid = UnityEngine.Object.FindFirstObjectByType<TileGrid>();
            if (tileGrid != null)
            {
                // FindAllTiles with always-true predicate to get all tiles
                var allTiles = tileGrid.FindAllTiles(_ => true);
                stats.totalTiles = allTiles.Count;
                foreach (var tile in allTiles)
                {
                    var style = tile.Archetype?.Style ?? TileStyle.Grass;
                    switch (style)
                    {
                        case TileStyle.Grass:    stats.grassTiles++; break;
                        case TileStyle.Forest:   stats.forestTiles++; break;
                        case TileStyle.Mountain: stats.mountainTiles++; break;
                        case TileStyle.Water:    stats.waterTiles++; break;
                        case TileStyle.Coast:    stats.coastTiles++; break;
                    }
                }
            }

            // Count resource nodes by type
            var nodes = UnityEngine.Object.FindObjectsByType<ResourceNode>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var node in nodes)
            {
                if (node == null) continue;
                switch (node.resourceType)
                {
                    case ResourceNode.ResourceType.Tree:  stats.treeCount++; break;
                    case ResourceNode.ResourceType.Stone:
                        if (node.isMineShaft) stats.mineCount++;
                        else stats.stoneCount++;
                        break;
                    case ResourceNode.ResourceType.Seed:  stats.seedCount++; break;
                    case ResourceNode.ResourceType.Crop:  stats.cropCount++; break;
                }
            }

            _metadata.mapStats = stats;

            // Capture map seed from TWCBridge
            var bridge = UnityEngine.Object.FindFirstObjectByType<TWCBridge>();
            if (bridge != null)
                _metadata.mapSeed = bridge.MapSeed;
        }

        /// <summary>
        /// Writes the final metadata file with run results.
        /// </summary>
        public void FinalizeAndWrite(string abortReason, LLMSessionStats sessionStats)
        {
            _metadata.endTime = DateTime.Now.ToString("o");
            _metadata.abortReason = abortReason;
            _metadata.finalTick = SimTickTracker.CurrentTick;
            _metadata.elapsedGameTimeSeconds = _metadata.finalTick * SimTickTracker.TickQuantum;
            _metadata.elapsedRealTimeSeconds = Time.realtimeSinceStartup - _startRealTime;
            _metadata.sessionStats = sessionStats;
            _metadata.environment = CaptureEnvironment(sessionStats);

            // Capture LLM settings at finalization (LLMController is guaranteed ready by now)
            var llm = LLMController.Instance;
            if (llm != null)
            {
                _metadata.actualModel = llm.CurrentModel;
                _metadata.llmSettings = new LLMSettings
                {
                    useConversationMemory = llm.UseConversationMemory,
                    memoryPairs = llm.MemoryPairs,
                    thinkMode = llm.CurrentThinkMode.ToString(),
                    contextSize = llm.ContextSize,
                    decisionDebounceSeconds = llm.DecisionDebounceDelay,
                    fallbackIntervalSeconds = llm.BatchDecisionInterval,
                    promptStyle = GlobalSettings.Instance != null ? GlobalSettings.Instance.PromptStyle.ToString() : "Normal"
                };
            }

            string json = JsonUtility.ToJson(_metadata, true);
            string path = Path.Combine(_outputDir, "run_metadata.json");
            File.WriteAllText(path, json);
        }

        /// <summary>Reads the current commit from .git next to the Assets folder. "unknown" in builds or on failure.</summary>
        private static string ReadGitCommit()
        {
            try
            {
                string gitDir = FindGitDir();
                if (gitDir == null) return "unknown";
                string head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
                if (!head.StartsWith("ref: ")) return head; // detached HEAD

                string refName = head.Substring(5);
                string refPath = Path.Combine(gitDir, refName);
                if (File.Exists(refPath)) return File.ReadAllText(refPath).Trim();

                // Ref may only exist in packed-refs
                string packed = Path.Combine(gitDir, "packed-refs");
                if (File.Exists(packed))
                {
                    var line = File.ReadAllLines(packed).FirstOrDefault(l => l.EndsWith(" " + refName));
                    if (line != null) return line.Split(' ')[0];
                }
            }
            catch (Exception) { /* fall through */ }
            return "unknown";
        }

        /// <summary>
        /// In the Editor dataPath is &lt;repo&gt;/Assets; in a player build it is &lt;build&gt;/&lt;name&gt;_Data.
        /// Walking up finds the repo as long as the build folder lies inside it (e.g. &lt;repo&gt;/Build).
        /// </summary>
        private RunEnvironment CaptureEnvironment(LLMSessionStats stats)
        {
            float real = _metadata.elapsedRealTimeSeconds;
            float llmWait = stats != null ? (float)stats.totalResponseTime : 0f;
            float running = real - llmWait;
            return new RunEnvironment
            {
                isEditor = Application.isEditor,
                platform = Application.platform.ToString(),
                machineName = SystemInfo.deviceName,
                operatingSystem = SystemInfo.operatingSystem,
                processorType = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                systemMemoryMB = SystemInfo.systemMemorySize,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                maximumDeltaTime = Time.maximumDeltaTime,
                avgFps = real > 0f ? (Time.frameCount - _startFrame) / real : 0f,
                llmWaitSeconds = llmWait,
                llmWaitShare = real > 0f ? llmWait / real : 0f,
                simSpeedWhileRunning = running > 1f ? _metadata.elapsedGameTimeSeconds / running : 0f
            };
        }

        private static string FindGitDir()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
