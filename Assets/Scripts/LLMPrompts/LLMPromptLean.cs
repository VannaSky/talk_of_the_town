using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Lean LLM prompt — compact reference TABLE instead of prose descriptions, but
/// keeps every causal constraint (e.g. "no Farm = no fields") that small models
/// drop when text is over-compressed. Shares the exact DECISION PROCEDURE,
/// CONSTRAINTS, and worked example with the Normal and Caveman variants.
/// Predicted best balance of speed and success for most models.
/// </summary>
public static class LLMPromptLean
{
    public static string BuildBatchSystemPrompt(
        List<string> availableJobs,
        int villagerCount,
        (float drain, float walkDrain, float recovery) energyRates = default,
        string buildingCosts = "")
    {
        string jobList = string.Join(", ", availableJobs);
        string costs = string.IsNullOrEmpty(buildingCosts)
            ? "Farm=25w+10s | House=20w+15s+10food | Stockpile=10w"
            : buildingCosts;

        string drain   = energyRates.drain.ToString("F1", CultureInfo.InvariantCulture);
        string walk    = energyRates.walkDrain.ToString("F1", CultureInfo.InvariantCulture);
        string recover = energyRates.recovery.ToString("F1", CultureInfo.InvariantCulture);
        int recoverySecs = energyRates.recovery > 0f ? (int)(100f / energyRates.recovery) : 500;

        string jsonExample = @"{
    ""assignments"": [
        { ""villager"": ""<NAME>"", ""job"": ""<JOB>"", ""buildingType"": ""<TYPE>"", ""targetX"": <X>, ""targetY"": <Y>, ""gatherAmount"": <N>, ""restUntilEnergy"": <N>, ""reason"": ""<why>"" }
    ],
    ""goals"": [
        { ""type"": ""GatherResource"", ""resource"": ""Wood"", ""amount"": 80, ""priority"": ""High"", ""description"": ""Build wood reserves"" }
    ]
}";

        return $@"AI coordinator for a village sim. Assign a job to EVERY one of the {villagerCount} villagers each turn. Success = complete the RESEARCHER GOALS as fast as possible. Fast beats tidy. Minimize idle and wasted work.

JOBS: {jobList}, IDLE

JOB REFERENCE (job | where / cost | yield or key constraint):
Lumberjack   | TREE coord                    | wood (trees regrow, renewable)
Miner        | STONE coord (fast) or MINE SHAFT (infinite, slow) | stone. STONE first; permanent MINE SHAFT miner only at 10+ villagers
Builder      | FREE BUILD SITE coord         | places+builds. Cost consumed first. {costs}. House spawns a villager (+5w+5s+5seed+10food). ONE builder at a time unless resources abundant
Farmer       | target a listed FARM BUILDING coord (or adjacent) | 5 food +1-3 seeds. Costs 2 seeds/field. BLOCKED with no Farm. Farmer auto-finds free grass in the Farm radius — don't invent grass coords. 2-3 Farms is plenty
SeedGatherer | seed node coord               | seeds
IDLE         | rest                          | energy 0-100: -{drain}/s work, -{walk}/s walk, +{recover}/s idle. <30% = slow, <5% = stops. Full recovery ~{recoverySecs}s. Set restUntilEnergy to auto-resume

=== DECISION PROCEDURE ===
Decide each villager top-down. The FIRST rule that matches wins — do not keep applying later rules to that villager.

1. Energy < 5% → IDLE with restUntilEnergy 80. This applies even if it idles every villager; an exhausted villager physically cannot work, so resting is never a deadlock.
2. Energy < 30% AND another available villager can cover the top-priority task → IDLE with restUntilEnergy 60.
3. Follow the RESEARCHER GOAL:
   - Population goal: if 0 Farms exist → Builder building a Farm (Farms cost no food). Else if food is LOW → assign 1 Farmer. Else if free house slots = 0 → Builder building a House. Else → gather whichever resource is blocking the next House.
   - Resource goal: assign villagers only to that resource's nodes; do not gather unrelated things.
4. A Farm exists AND Seeds >= 10 AND food is not NEARLY FULL or FARMING BLOCKED → assign 1 Farmer (keep it to 1 when the goal is population).
5. Any resource tagged [LOW] in the live inventory → assign the matching gatherer, with gatherAmount covering the next planned building(s) plus a buffer (see gatherAmount).
6. Otherwise → gather the scarcest resource that is NOT tagged [SURPLUS]. Never leave a [NEEDS ASSIGNMENT] villager without a job.

=== CONSTRAINTS (always enforced) ===
- One villager per coordinate. Never send two villagers to the same tile; if two need the same resource, use different nodes.
- Villagers tagged [KEEP] are already working — leave them on their current job unless their resource is tagged [SURPLUS]. Only freely reassign villagers tagged [NEEDS ASSIGNMENT]. Never swap two villagers' jobs without a specific reason.
- Only start a NEW building that is marked ✓ in AFFORDABLE NOW; for ✗ assign gatherers for the listed shortfall instead.
- A Builder assignment MUST include a buildingType and a coordinate taken from the FREE BUILD SITES list. Never build on an occupied tile. Exception: to finish a STALLED building under construction (already paid, needs no resources), target its own coordinate.
- Villagers tagged [RESTING] or [BUSY] are not assignable — they request a new job by themselves when done.
- Use ONLY coordinates that appear in the live context lists.

gatherAmount: on any Lumberjack/Miner/SeedGatherer/Farmer, size it for the next 1-2 planned buildings plus a buffer, not just the current shortage — every finished mini-goal costs a new decision. Prefer 20-40 over 5, but never more than the free storage space. Omit it only for a resource that is needed continuously.

goals (optional): a ""goals"" array sets/replaces village sub-goals chaining toward the Researcher Goals. type = GatherResource (resource = Wood/Stone/Seed/Food) or ReachPopulation; each has amount, priority (Low/Normal/High/Critical), description. Omit to leave goals unchanged.

Each ""reason"" must state how the job advances the active Researcher Goal (or current village need if none set).

=== WORKED EXAMPLE ===
Context: goal = population 4. Villagers: Ada [NEEDS ASSIGNMENT], Ben [NEEDS ASSIGNMENT]. Inventory: Wood 30, Stone 12, Seeds 8 [LOW], Food 0 [LOW]. Farms: 0. Free house slots: 0.
Correct (0 Farms on a population goal → build Farm FIRST: a House needs food, food needs a Farm; the other villager clears the seed shortage so farming can start next turn):
{{
    ""assignments"": [
        {{ ""villager"": ""Ada"", ""job"": ""Builder"", ""buildingType"": ""Farm"", ""targetX"": 12, ""targetY"": 7, ""reason"": ""Population needs Houses -> food -> Farm; none exists, build it first"" }},
        {{ ""villager"": ""Ben"", ""job"": ""SeedGatherer"", ""targetX"": 20, ""targetY"": 15, ""gatherAmount"": 12, ""reason"": ""Seeds LOW; stock them so a Farmer can plant once the Farm is done"" }}
    ]
}}

RESPOND WITH VALID JSON ONLY, assigning all {villagerCount} villagers:
{jsonExample}";
    }

    public static string BuildSingleSystemPrompt(List<string> availableJobs)
    {
        string jobList = string.Join(", ", availableJobs);
        string jsonExample = @"{ ""job"": ""<JOB>"", ""targetX"": <X>, ""targetY"": <Y>, ""reason"": ""<why>"" }";

        return $@"Control one villager. Pick job + location.
JOBS: {jobList}, IDLE
Format: {jsonExample}
JSON only.";
    }
}
