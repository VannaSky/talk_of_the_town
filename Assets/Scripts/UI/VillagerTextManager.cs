using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class VillagerTextManager : MonoBehaviour
    {


        [SerializeField] private string villagerName;
        [SerializeField] private string jobName;
        [SerializeField] private string promptReason;
        [SerializeField] private string currentState;
      

        [SerializeField] private VillagerBrain _villagerBrain;
        [SerializeField] private Villager _villager;

        public void SetVillager(Villager villager, VillagerBrain brain)
        {
            _villager = villager;
            _villagerBrain = brain;
        }

        [SerializeField] private TextMeshProUGUI villagerNameText;
        [SerializeField] private TextMeshProUGUI jobNameText;
        [SerializeField] private TextMeshProUGUI promptReasonText;
        [SerializeField] private TextMeshProUGUI currentStateText;
        [SerializeField] private TextMeshProUGUI energyText;

        // Update is called once per frame
        void Update()
        {
            if (_villager == null || _villagerBrain == null) return;

            villagerName = _villager.villagerName;
            jobName = _villagerBrain.lastDecision?.jobName ?? "—";
            promptReason = _villagerBrain.lastDecision?.reason ?? "—";
            currentState = _villagerBrain.currentState;

            villagerNameText.text = villagerName;
            jobNameText.text = jobName;
            promptReasonText.text = promptReason;
            currentStateText.text = currentState;

            if (energyText != null)
            {
                int e = _villager.EnergyPercent;
                energyText.text = $"Energy: {e}%";
                energyText.color = e < 5 ? Color.red : e < 30 ? Color.yellow : Color.green;
            }
        }
    }
}