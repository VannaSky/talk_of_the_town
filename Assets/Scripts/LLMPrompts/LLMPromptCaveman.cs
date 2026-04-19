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

        return $@"Village AI. Assign ALL {villagerCount} villagers. No two same spot.

JOBS: {jobList}, IDLE
- Lumberjack: chop trees→wood. Target TREE.
- Miner: mine stone. Target STONE.
- Builder: build. Need wood+stone. buildingType: House(+villager), Stockpile(+inventory), Farm(+farming). Pick by need.
- Farmer: plant(need seeds)+harvest→food+seeds. Self-sustaining. Target FARM/grass. MAIN FOOD JOB.
- SeedGatherer: gather seeds from nodes.
- IDLE: rest.

PRIORITY:
1. Seeds>=10→Farmer(1/20seeds). Food=top.
2. Wood>=20+Stone>=10→Builder(places own foundation). buildingType required: House(pop low), Stockpile(inventory full), Farm(need food).
3. Low only: Wood<10→Lumberjack, Stone<10→Miner, Seeds<10→SeedGatherer.
4. Surplus→stop: Wood>50 no Lumberjack, Seeds>30 no SeedGatherer(farmers make seeds).

RULES:
- Different coords per villager. Never same spot.
- Surplus=switch to Farmer/Builder.

GOALS(opt): ""goals"" array replaces existing. type=GatherResource/ReachPopulation, resource=Wood/Stone/Seed/Food, amount, priority=Low/Normal/High/Critical, description.

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
