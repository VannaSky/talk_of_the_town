// Benchmark run overview + sanity checks.
// Usage:  node Tools/BenchmarkAnalysis/analyze_runs.js [filter]
//   filter = substring of the run folder name (default: all matrix runs, i.e. folders not starting with "_" or "test_")
// Reads %USERPROFILE%/AppData/LocalLow/DefaultCompany/talk_of_the_town/BenchmarkRuns.
//
// Per run: result, cost, environment, format errors, assignment outcomes, plus the checks for the bugs fixed on
// 2026-10-03 (pause during LLM calls, villagers walking in place, parser fallbacks, model digest changes).

const fs = require('fs');
const path = require('path');

const root = path.join(process.env.USERPROFILE, 'AppData', 'LocalLow', 'DefaultCompany', 'talk_of_the_town', 'BenchmarkRuns');
const filter = process.argv[2];

const readJsonl = f => fs.existsSync(f)
    ? fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l))
    : [];

// Minimal CSV parser for villager_timeseries.csv (job_status may contain quoted commas)
function parseCsvLine(line) {
    const out = []; let cur = ''; let quoted = false;
    for (let i = 0; i < line.length; i++) {
        const ch = line[i];
        if (ch === '"') { if (quoted && line[i + 1] === '"') { cur += '"'; i++; } else quoted = !quoted; continue; }
        if (ch === ',' && !quoted) { out.push(cur); cur = ''; continue; }
        cur += ch;
    }
    out.push(cur);
    return out;
}

// Villagers whose position did not change for >= minTicks while their animation state was "walking"
function findWalkingInPlace(dir, minTicks = 150) {
    const f = path.join(dir, 'villager_timeseries.csv');
    if (!fs.existsSync(f)) return [];
    const rows = fs.readFileSync(f, 'utf8').trim().split('\n').slice(1).map(parseCsvLine);
    const state = {}; const hits = [];
    const close = (n, s) => { if (s && s.walk >= minTicks) hits.push(`${n}@${s.pos} t${s.t0}-${s.last} (${s.walk} ticks)`); };
    for (const r of rows) {
        const name = r[2], tick = +r[0], pos = r[3] + ',' + r[4], walking = r[9] === 'walking';
        const s = state[name];
        if (!s || s.pos !== pos) { close(name, s); state[name] = { pos, t0: tick, last: tick, walk: 0 }; continue; }
        if (walking) s.walk += tick - s.last;
        s.last = tick;
    }
    for (const n in state) close(n, state[n]);
    return hits;
}

const FORMAT_KEYS = ['invalidJobs', 'invalidTargets', 'strippedBuildingTypes', 'strippedRestTargets', 'strippedGatherAmounts',
    'bracketedJobs', 'missingVillagers', 'unknownVillagers', 'duplicateAssignments', 'parseFailures', 'flatDictFallbacks'];

const dirs = fs.readdirSync(root)
    .filter(d => fs.statSync(path.join(root, d)).isDirectory())
    .filter(d => filter ? d.includes(filter) : !d.startsWith('_') && !d.startsWith('test_'))
    .sort();

const digests = {};
const warnings = [];

for (const d of dirs) {
    const dir = path.join(root, d);
    const metaFile = path.join(dir, 'run_metadata.json');
    if (!fs.existsSync(metaFile)) { console.log(`${d}: no run_metadata.json (running or aborted)`); continue; }

    const m = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
    const s = m.sessionStats || {}, e = m.environment || {}, mi = m.modelInfo || {};
    const dec = readJsonl(path.join(dir, 'llm_decisions.jsonl'));
    const ev = readJsonl(path.join(dir, 'world_events.jsonl'));

    const goals = ev.filter(x => x.eventType === 'goal_completed')
        .map(x => `${x.simTick}:${(x.details.description || '').replace(/^(Gather|Reach|Build) /, '')}`).join(' | ');
    const built = {};
    ev.filter(x => x.eventType === 'building_completed').forEach(x => built[x.details.buildingType] = (built[x.details.buildingType] || 0) + 1);
    const outcomes = {};
    ev.filter(x => x.eventType === 'assignment_outcome').forEach(x => outcomes[x.details.outcome] = (outcomes[x.details.outcome] || 0) + 1);
    const unstuck = ev.filter(x => x.eventType === 'villager_unstuck');
    const tickMovedDuringCall = dec.filter(x => x.requestStartTick !== undefined && x.requestStartTick !== x.simTick).length;
    const walkingInPlace = findWalkingInPlace(dir);
    const fmt = FORMAT_KEYS.filter(k => s[k]).map(k => `${k}=${s[k]}`).join(' ') || '-';

    console.log(`\n== ${d}  [${m.modelName} | ${m.promptStyle} | ${m.mapFile} | git ${String(m.gitCommit).slice(0, 8)}]`);
    console.log(`   result   finalTick ${m.finalTick} (${m.abortReason})  real ${Math.round(m.elapsedRealTimeSeconds)}s`);
    console.log(`   goals    ${goals || '-'}`);
    console.log(`   built    ${JSON.stringify(built)}`);
    console.log(`   cost     calls ${s.totalRequests} (failed ${s.failedRequests})  avg ${(s.totalResponseTime / Math.max(1, s.totalRequests)).toFixed(2)}s` +
        `  prompt/call ${Math.round(s.totalPromptTokens / Math.max(1, s.totalRequests))}  resp/call ${Math.round(s.totalResponseTokens / Math.max(1, s.totalRequests))}` +
        `  thinkChars ${s.totalThinkingChars}  maxPrompt ${s.maxPromptTokens}  skipFB ${s.skippedFallbacks} skipTr ${s.skippedTriggers}`);
    console.log(`   env      editor ${e.isEditor}  fps ${Math.round(e.avgFps)} (cap ${e.targetFrameRate}, render 1/${e.renderFrameInterval}, hidden ${e.worldHidden})` +
        `  sim ${(e.simSpeedWhileRunning || 0).toFixed(1)}x  llmWait ${Math.round(100 * (e.llmWaitShare || 0))}%`);
    console.log(`   format   ${fmt}  goalsSet ${s.llmGoalSets}  nearCtx ${s.callsNearContextLimit}  truncated ${s.truncatedResponses}`);
    console.log(`   outcome  ${JSON.stringify(outcomes)}`);

    // Checks
    if (e.isEditor) warnings.push(`${d}: ran in the Editor`);
    if (tickMovedDuringCall) warnings.push(`${d}: ${tickMovedDuringCall} calls where ticks advanced during the LLM call (pause broken?)`);
    if (walkingInPlace.length) warnings.push(`${d}: walking in place: ${walkingInPlace.join('; ')}`);
    if (unstuck.length) warnings.push(`${d}: ${unstuck.length} villager_unstuck (${unstuck.filter(x => x.details.fallback).length} fallback)`);
    if ((outcomes.fallback_idle || 0) > 0) warnings.push(`${d}: ${outcomes.fallback_idle} fallback_idle (parse failures ${s.parseFailures}) — check raw responses`);
    if (s.callsNearContextLimit || s.truncatedResponses) warnings.push(`${d}: context near limit ${s.callsNearContextLimit}, truncated ${s.truncatedResponses}`);
    if (m.abortReason !== 'goals_reached') warnings.push(`${d}: ended with ${m.abortReason} at tick ${m.finalTick}`);
    if (mi.digest) {
        (digests[m.modelName] = digests[m.modelName] || new Set()).add(mi.digest);
    } else warnings.push(`${d}: no model digest`);
}

for (const [model, set] of Object.entries(digests))
    if (set.size > 1) warnings.push(`MODEL CHANGED during the matrix: ${model} has ${set.size} digests: ${[...set].map(x => x.slice(0, 12)).join(', ')}`);

console.log(`\n${dirs.length} folders checked.`);
console.log(warnings.length ? `WARNINGS:\n - ${warnings.join('\n - ')}` : 'No warnings.');
