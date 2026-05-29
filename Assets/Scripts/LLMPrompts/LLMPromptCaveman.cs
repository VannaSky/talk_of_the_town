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
Lumberjack→wood, target TREE (trees regrow, renewable)
Miner→stone, target STONE (fast) or MINE SHAFT (infinite/very slow). STONE first while available. At 10+ villagers: keep 1 miner at MINE SHAFT permanently.
Builder→place+build. Need wood+stone. buildingType: House(villager auto-spawns on finish; spawn costs 5w+5s+5seed+10food from stores)/Stockpile/Farm
Farmer→plant near Farm(costs 2seeds/field)+harvest→5food+1-3seeds. NEEDS Farm to plant! No farm=no fields. Crops regrow→2-3 farms enough.
SeedGatherer→seeds from nodes
IDLE→rest

PRIORITY:
0. RESEARCHER GOALS→override all. Population goal→Houses first. Resource goal→gather that resource. Stay focused.
1. Seeds>=10+food not NEARLY FULL+not FARMING BLOCKED→1 Farmer(population goal→max 1). Food NEARLY FULL/BLOCKED→no Farmers, no Farms.
2. Wood>=20+Stone>=10→Builder. House(population goal→priority)/Stockpile(inv near full or 2+ free slots)/Farm(ONLY if 0 farms OR food critical — NOT if food high). 2+ free slots→no more Houses.
3. Low only: Wood<10→Lumberjack, Stone<10→Miner, Seeds<10→SeedGatherer
4. Surplus→stop: Wood>50 no Lumberjack, Seeds>30→farm instead.

RULES:
Diff coords each villager. No same spot.
Surplus→switch Farmer/Builder.
[KEEP]=working→no reassign. [NEEDS ASSIGNMENT]=assign only these. No job swaps.

RESEARCHER GOALS(if present): fixed targets from researcher. Cannot change. Override default priorities.
GOALS(opt): ""goals"" replaces existing. type=GatherResource/ReachPopulation, resource=Wood/Stone/Seed/Food, amount, priority=Low/Normal/High/Critical, description.

reason=how this job advances researcher goal(or village need if no goal set).

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
