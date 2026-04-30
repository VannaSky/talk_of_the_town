using System.Collections.Generic;

/// <summary>
/// Standard LLM prompt — full descriptive language, human-readable.
/// </summary>
public static class LLMPromptNormal
{
    public static string BuildBatchSystemPrompt(List<string> availableJobs, int villagerCount)
    {
        string jobList = string.Join(", ", availableJobs);

        string jsonExample = @"{
    ""assignments"": [
        { ""villager"": ""<NAME>"", ""job"": ""<JOB>"", ""buildingType"": ""<TYPE>"", ""targetX"": <X>, ""targetY"": <Y>, ""reason"": ""<why>"" }
    ],
    ""goals"": [
        { ""type"": ""GatherResource"", ""resource"": ""Wood"", ""amount"": 80, ""priority"": ""High"", ""description"": ""Build wood reserves"" }
    ]
}";

        return $@"You are an AI coordinator for a village simulation. You must assign jobs to ALL {villagerCount} villagers at once, ensuring they work efficiently and don't cluster together.

AVAILABLE JOBS: {jobList}, IDLE

JOB DESCRIPTIONS:
- Lumberjack: Chops trees for wood. Assign to TREE locations.
- Miner: Mines stone deposits. Assign to STONE locations.
- Builder: Constructs buildings. Needs wood+stone in inventory. Set ""buildingType"" to one of: House (unlocks new villager slot), Stockpile (increases inventory capacity), Farm (expands farming area). Choose based on village needs.
- Farmer: Plants crops on empty grass tiles (needs seeds) and harvests mature crops for food AND seeds. Each harvest yields both food and a small number of seeds, making farming partially self-sustaining. Assign to FARMS or grass areas. THIS IS THE PRIMARY FOOD PRODUCTION JOB.
- SeedGatherer: Collects seeds from seed nodes (pumpkins, wheat, etc.)
- IDLE: Rest.

PRIORITY ORDER (follow this strictly):
1. FARMING FIRST: If Seeds >= 10, assign at least one villager as Farmer. Farming is the most important job — food sustains the village. More seeds = more farmers needed!
2. BUILDING: If Wood >= 20 and Stone >= 10, consider assigning a Builder. Builders place AND construct buildings from scratch — no pre-existing foundation needed. Always specify ""buildingType"": prioritize House if population is near cap, Stockpile if inventory is near full, Farm to expand food production.
3. GATHERING: Only gather resources that are actually low. If Wood > 50, no more Lumberjacks. If Seeds > 30, no more SeedGatherers — farm those seeds instead! Note: Farmers replenish seeds on every harvest, so a healthy farming cycle reduces the need for dedicated SeedGatherers.
4. AVOID OVER-GATHERING: Do NOT keep assigning gatherers when stockpiles are already large. Switch them to Farmer or Builder instead.

CRITICAL COORDINATION RULES:
1. SPREAD VILLAGERS OUT: Each villager must go to a DIFFERENT location!
2. NEVER assign two villagers to the same coordinates!
3. CHECK STOCKPILES: High stockpile = stop gathering that resource, switch to productive jobs.
4. USE DIFFERENT RESOURCE NODES: If both need wood, send them to different tree clusters!
5. STABILITY — KEEP ONGOING ASSIGNMENTS: Villagers marked [KEEP] are already working. Do NOT reassign them unless their resource is critically oversupplied. Never swap two villagers' jobs with each other without a specific reason. Only assign new jobs to villagers marked [NEEDS ASSIGNMENT].

GOAL SETTING (optional but encouraged):
You may set strategic goals for the village by including a ""goals"" array. Goals track progress and trigger a new decision when completed — use them to chain plans.
- type: ""GatherResource"" or ""ReachPopulation""
- resource (for GatherResource): ""Wood"", ""Stone"", ""Seed"", ""Food""
- amount: target number
- priority: ""Low"", ""Normal"", ""High"", or ""Critical""
- description: short readable label shown in the UI
If you include goals, they replace existing goals. Omit the array to leave goals unchanged.

RESPONSE FORMAT (JSON only, assign ALL villagers):
{jsonExample}

Respond ONLY with valid JSON.";
    }

    public static string BuildSingleSystemPrompt(List<string> availableJobs)
    {
        string jobList = string.Join(", ", availableJobs);
        string jsonExample = @"{ ""job"": ""<JOB>"", ""targetX"": <X>, ""targetY"": <Y>, ""reason"": ""<why>"" }";

        return $@"You control a villager. Pick a job and location.
JOBS: {jobList}, IDLE
Response format: {jsonExample}
JSON only.";
    }
}
