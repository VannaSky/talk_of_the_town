using System.Threading.Tasks;

/// <summary>
/// Interface für Entities, die LLM-Entscheidungen treffen können
/// </summary>
public interface ILLMDecisionMaker
{
    /// <summary>
    /// Erstellt einen Entscheidungs-Prompt basierend auf aktuellem Zustand
    /// </summary>
    string BuildDecisionPrompt();
    
    /// <summary>
    /// Verarbeitet die LLM-Response und führt Aktion aus
    /// </summary>
    void ProcessDecision(string llmResponse);
}