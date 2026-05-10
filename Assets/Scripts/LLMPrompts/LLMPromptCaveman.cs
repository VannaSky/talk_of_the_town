using System.Collections.Generic;

/// <summary>
/// Caveman-style LLM prompt — minimal words, token-efficient, same logic as Normal.
/// </summary>
public static class LLMPromptCaveman
{
    public static string BuildBatchSystemPrompt(List<string> availableJobs, int villagerCount)
    {
        string jobList = string.Join(", ", availableJobs);

        string jsonExample = @"{""assignments"":[{""villager"":""<NAME>"",""job"":""<JOB>"",""buildingType"":""<TYPE>"",""targetX"":<X>,""targetY"":<Y>,""reason"":""<why>""}],""goals"":[{""type"":""GatherResource"",""resource"":""Wood"",""amount"":80,""priority"":""High"",""description"":""build wood""}]}";

        return $@"Assign ALL {villagerCount} villagers. No 2 same spot.

JOBS: {jobList}, IDLE
Lumberjack→wood, target TREE
Miner→stone, target STONE
Builder→place+build. Need wood+stone. buildingType: House(villager auto-spawns on finish)/Stockpile/Farm
Farmer→plant(seeds)+harvest→food+seeds. Main food. Target FARM/grass. Crops regrow→2-3 farms enough.
SeedGatherer→seeds from nodes
IDLE→rest

PRIORITY:
1. Seeds>=10→1+ Farmer. Farmer harvest→food+seeds(self-sustaining). Healthy farm cycle→less SeedGatherers needed.
2. Wood>=20+Stone>=10→Builder. buildingType: House(auto-spawns villager)/Stockpile(inv near full or 2+ free slots)/Farm(need food). ROTATE: if 2+ free house slots exist→build Stockpile/Farm, not more Houses.
3. Low only: Wood<10→Lumberjack, Stone<10→Miner, Seeds<10→SeedGatherer
4. Surplus→stop: Wood>50 no Lumberjack, Seeds>30 no SeedGatherer→farm instead.

RULES:
Diff coords each villager. No same spot.
Surplus→switch Farmer/Builder.
[KEEP]=working→no reassign. [NEEDS ASSIGNMENT]=assign only these. No job swaps.

GOALS(opt): ""goals"" replaces existing. type=GatherResource/ReachPopulation, resource=Wood/Stone/Seed/Food, amount, priority=Low/Normal/High/Critical, description.

JSON ONLY:
{jsonExample}";
    }

    public static string BuildSingleSystemPrompt(List<string> availableJobs)
    {
        string jobList = string.Join(", ", availableJobs);
        string jsonExample = @"{""job"":""<JOB>"",""targetX"":<X>,""targetY"":<Y>,""reason"":""<why>""}";

        return $@"Pick job+location. JOBS:{jobList},IDLE. JSON:{jsonExample}";
    }
}
