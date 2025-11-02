// using System.Threading.Tasks;
// using UnityEngine;
//
// /// <summary>
// /// Verbindet euren Villager mit dem LLM Controller
// /// </summary>
// public class VillagerLLMBridge : MonoBehaviour, ILLMDecisionMaker
// {
//     [Header("References")]
//     [SerializeField] private Villager villager; // Euer existierendes Villager-Script
//     
//     [Header("Decision Settings")]
//     [SerializeField] private float decisionInterval = 30f;
//     [SerializeField] private bool autoDecide = true;
//     
//     private float decisionTimer = 0f;
//     
//     private void Update()
//     {
//         if (!autoDecide || LLMController.Instance == null || !LLMController.Instance.IsReady)
//             return;
//         
//         decisionTimer += Time.deltaTime;
//         if (decisionTimer >= decisionInterval)
//         {
//             decisionTimer = 0f;
//             MakeDecision();
//         }
//     }
//     
//     public async void MakeDecision()
//     {
//         // 1. Prompt erstellen
//         string prompt = BuildDecisionPrompt();
//         
//         // 2. LLM anfragen
//         var response = await LLMController.Instance.GetResponse(prompt);
//         
//         // 3. Response verarbeiten
//         if (response.success)
//         {
//             ProcessDecision(response.content);
//         }
//         else
//         {
//             Debug.LogWarning($"[{villager.VillagerName}] Decision failed: {response.error}");
//             // Fallback-Logik
//             ExecuteFallbackAction();
//         }
//     }
//     
//     public string BuildDecisionPrompt()
//     {
//         // TODO: Mit eurem Job System verbinden
//         return $@"You are {villager.VillagerName}, a {villager.CurrentJob}.
//
// YOUR STATUS:
// Hunger: {villager.Needs.Hunger}/100
// Thirst: {villager.Needs.Thirst}/100
// Sleep: {villager.Needs.Sleep}/100
// Job: {villager.CurrentJob} (Level {villager.JobLevel})
//
// VILLAGE STATUS:
// Population: [TODO: Get from SimulationState]
// Food: [TODO: Get from SimulationState]
//
// Choose ONE action: EAT, DRINK, SLEEP, GATHER_FOOD, GATHER_WOOD
//
// Format: ACTION: <choice>";
//     }
//     
//     public void ProcessDecision(string llmResponse)
//     {
//         // Einfacher Parser
//         string action = ParseAction(llmResponse);
//         
//         Debug.Log($"[{villager.VillagerName}] Decided: {action}");
//         
//         // TODO: Mit eurem Job System verbinden
//         // villager.ExecuteAction(action);
//     }
//     
//     private string ParseAction(string response)
//     {
//         var match = System.Text.RegularExpressions.Regex.Match(
//             response, 
//             @"ACTION:\s*(\w+)", 
//             System.Text.RegularExpressions.RegexOptions.IgnoreCase
//         );
//         
//         if (match.Success)
//             return match.Groups[1].Value.ToUpper();
//         
//         return "EAT"; // Fallback
//     }
//     
//     private void ExecuteFallbackAction()
//     {
//         // Simple Regel: Niedrigster Need wird erfüllt
//         if (villager.Needs.Hunger < 40)
//             Debug.Log($"[{villager.VillagerName}] Fallback: EAT");
//         else if (villager.Needs.Thirst < 40)
//             Debug.Log($"[{villager.VillagerName}] Fallback: DRINK");
//         else
//             Debug.Log($"[{villager.VillagerName}] Fallback: GATHER_FOOD");
//     }
// }