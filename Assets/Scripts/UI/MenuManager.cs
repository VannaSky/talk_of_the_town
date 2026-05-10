using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace UI
{
    public class MenuManager : MonoBehaviour
    {

        [SerializeField] private int wood = 0;
        [SerializeField] private int stone = 0;
        [SerializeField] private int seed = 0;
        [SerializeField] private int food = 0;
        [SerializeField] private int population = 0;

        [SerializeField] private string model;

        [Header("SessionStats")]
        [SerializeField] private int tokens;
        [SerializeField] private int prompt;
        [SerializeField] private int response;
        [SerializeField] private double time;


        [SerializeField] private TextMeshProUGUI woodText;
        [SerializeField] private TextMeshProUGUI stoneText;
        [SerializeField] private TextMeshProUGUI seedText;
        [SerializeField] private TextMeshProUGUI foodText;
        [SerializeField] private TextMeshProUGUI populationText;
        [SerializeField] private TextMeshProUGUI modelText;

        [SerializeField] private TextMeshProUGUI tokensText;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private TextMeshProUGUI responseText;
        [SerializeField] private TextMeshProUGUI timeText;

        private VillageState _villageState;
        private LLMController _llmController;

        void Start()
        {
            _villageState = VillageState.Instance;
            _llmController = LLMController.Instance;
        }

        void Update()
        {
            if (_villageState == null) _villageState = VillageState.Instance;
            if (_llmController == null) _llmController = LLMController.Instance;
            if (_villageState == null || _llmController == null) return;

            int cap = _villageState.InventoryCapacity;
            wood = _villageState.Wood;
            stone = _villageState.Stone;
            seed = _villageState.Seeds;
            food = _villageState.Food;
            population = _villageState.Villagers.Count;

            model = _llmController.CurrentModel;

            if (_llmController.LastMetrics != null)
            {
                tokens = _llmController.LastMetrics.totalTokens;
                prompt = _llmController.LastMetrics.promptEvalCount;
                response = _llmController.LastMetrics.evalCount;
                time = _llmController.LastMetrics.responseTime;
            }

            woodText.text = $"{wood}/{cap}";
            stoneText.text = $"{stone}/{cap}";
            seedText.text = $"{seed}/{cap}";
            foodText.text = $"{food}/{cap}";
            if (populationText != null)
                populationText.text = $"{population}/{_villageState.PopulationCap}";
            modelText.text = model;
            tokensText.text = $"Tokens: {tokens}";
            promptText.text = $"Prompts: {prompt}";
            responseText.text = $"Response: {response}";
            timeText.text = $"Time: {time.ToString(CultureInfo.CurrentCulture)}";
        }
    }
}
