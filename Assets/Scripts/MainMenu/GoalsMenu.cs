using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// GoalsMenu — lets the researcher configure Global Goals before starting a run.
    /// Each goal type has a Toggle (enable/disable) and a TMP_InputField (target amount).
    /// Click Apply to push the goals to GlobalGoals.Instance.
    /// Controls are locked automatically once the LLM controller starts (call LockGoals()).
    /// </summary>
    public class GoalsMenu : MonoBehaviour
    {
        public static GoalsMenu Instance { get; private set; }

        [SerializeField] private GameObject settingsMenu;

        [Header("Resource Goals")]
        [SerializeField] private Toggle         woodToggle;
        [SerializeField] private TMP_InputField woodInput;

        [SerializeField] private Toggle         stoneToggle;
        [SerializeField] private TMP_InputField stoneInput;

        [SerializeField] private Toggle         seedToggle;
        [SerializeField] private TMP_InputField seedInput;

        [SerializeField] private Toggle         foodToggle;
        [SerializeField] private TMP_InputField foodInput;

        [Header("Building Goal")]
        [SerializeField] private Toggle         buildingToggle;
        [SerializeField] private TMP_InputField buildingInput;

        [Header("Population Goal")]
        [SerializeField] private Toggle         populationToggle;
        [SerializeField] private TMP_InputField populationInput;

        [Header("Feedback")]
        [SerializeField] private TMP_Text statusText;

        private bool _locked;

        // ── Unity lifecycle ───────────────────────────────────────────────

        void Awake()
        {
            Instance = this;
        }

        void OnEnable()
        {
            // Sync toggle → input field enabled state whenever the menu opens
            RefreshInputStates();
            UpdateStatusText();
        }

        // ── Public UI callbacks (wire these in the Inspector) ─────────────

        public void OnWoodToggleChanged(bool value)      => SetInputInteractable(woodInput,       value);
        public void OnStoneToggleChanged(bool value)     => SetInputInteractable(stoneInput,      value);
        public void OnSeedToggleChanged(bool value)      => SetInputInteractable(seedInput,       value);
        public void OnFoodToggleChanged(bool value)      => SetInputInteractable(foodInput,       value);
        public void OnBuildingToggleChanged(bool value)  => SetInputInteractable(buildingInput, value);
        public void OnPopulationToggleChanged(bool value)=> SetInputInteractable(populationInput, value);

        /// <summary>
        /// Called by the Apply button. Validates inputs and pushes goals to GlobalGoals.
        /// </summary>
        public void OnApplyPressed()
        {
            if (_locked)
            {
                SetStatus("Goals are locked — simulation is running.");
                return;
            }

            var goals = new List<GlobalGoal>();

            // Resource goals
            TryAddResourceGoal(goals, woodToggle,       woodInput,       Tiles.ResourceType.Wood);
            TryAddResourceGoal(goals, stoneToggle,      stoneInput,      Tiles.ResourceType.Stone);
            TryAddResourceGoal(goals, seedToggle,       seedInput,       Tiles.ResourceType.Seed);
            TryAddResourceGoal(goals, foodToggle,       foodInput,       Tiles.ResourceType.Food);

            // Building goal (all building types combined)
            TryAddBuildingGoal(goals, buildingToggle, buildingInput);

            // Population goal
            TryAddPopulationGoal(goals, populationToggle, populationInput);

            if (GlobalGoals.Instance != null)
                GlobalGoals.Instance.SetGoals(goals);

            string msg = goals.Count > 0
                ? $"{goals.Count} global goal(s) applied."
                : "No goals set — simulation runs without win conditions.";
            SetStatus(msg);
        }

        public void OnBackPressed()
        {
            gameObject.SetActive(false);
            settingsMenu.SetActive(true);
        }

        /// <summary>
        /// Called by LLMController when the first decision fires.
        /// Locks the UI so goals cannot be changed mid-run.
        /// </summary>
        public void LockGoals()
        {
            _locked = true;
            SetAllInteractable(false);
            UpdateStatusText();
        }

        // ── Private helpers ───────────────────────────────────────────────

        private void TryAddResourceGoal(List<GlobalGoal> goals, Toggle toggle, TMP_InputField input, Tiles.ResourceType resource)
        {
            if (toggle == null || !toggle.isOn) return;
            if (!TryParseAmount(input, GlobalGoals.MaxResourceAmount, out int amount)) return;

            goals.Add(new GlobalGoal
            {
                type           = GlobalGoalType.ResourceAmount,
                targetResource = resource,
                targetAmount   = amount
            });
        }

        private void TryAddBuildingGoal(List<GlobalGoal> goals, Toggle toggle, TMP_InputField input)
        {
            if (toggle == null || !toggle.isOn) return;
            if (!TryParseAmount(input, GlobalGoals.MaxBuildingCount, out int amount)) return;

            goals.Add(new GlobalGoal
            {
                type         = GlobalGoalType.BuildingCount,
                targetAmount = amount
            });
        }

        private void TryAddPopulationGoal(List<GlobalGoal> goals, Toggle toggle, TMP_InputField input)
        {
            if (toggle == null || !toggle.isOn) return;
            if (!TryParseAmount(input, GlobalGoals.MaxPopulation, out int amount)) return;

            goals.Add(new GlobalGoal
            {
                type         = GlobalGoalType.PopulationCount,
                targetAmount = amount
            });
        }

        private bool TryParseAmount(TMP_InputField input, int max, out int amount)
        {
            amount = 0;
            if (input == null || string.IsNullOrWhiteSpace(input.text)) return false;
            if (!int.TryParse(input.text, out amount) || amount <= 0)   return false;

            // Clamp to allowed range and write back so the UI reflects it
            amount       = Mathf.Clamp(amount, 1, max);
            input.text   = amount.ToString();
            return true;
        }

        private void SetInputInteractable(TMP_InputField input, bool interactable)
        {
            if (input != null)
                input.interactable = !_locked && interactable;
        }

        private void RefreshInputStates()
        {
            SetInputInteractable(woodInput,       woodToggle      != null && woodToggle.isOn);
            SetInputInteractable(stoneInput,      stoneToggle     != null && stoneToggle.isOn);
            SetInputInteractable(seedInput,       seedToggle      != null && seedToggle.isOn);
            SetInputInteractable(foodInput,       foodToggle      != null && foodToggle.isOn);
            SetInputInteractable(buildingInput,   buildingToggle  != null && buildingToggle.isOn);
            SetInputInteractable(populationInput, populationToggle!= null && populationToggle.isOn);
        }

        private void SetAllInteractable(bool interactable)
        {
            Toggle[]         toggles = { woodToggle, stoneToggle, seedToggle, foodToggle,
                                         buildingToggle, populationToggle };
            TMP_InputField[] inputs  = { woodInput, stoneInput, seedInput, foodInput,
                                         buildingInput, populationInput };

            foreach (var t in toggles) if (t != null) t.interactable   = interactable;
            foreach (var i in inputs)  if (i != null) i.interactable   = interactable;
        }

        private void UpdateStatusText()
        {
            if (statusText == null) return;
            statusText.text = _locked
                ? "Goals locked — simulation is running."
                : "Set goals, then click Apply before starting.";
        }

        private void SetStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }
    }
}
