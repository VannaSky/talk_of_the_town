using System.Text;
using TMPro;
using UnityEngine;

namespace UI
{
    public class GoalsDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text goalsText;

        void Awake()
        {
            if (goalsText == null)
                goalsText = GetComponent<TMP_Text>();
        }

        void Update()
        {
            if (goalsText == null || VillageGoals.Instance == null) return;

            var goals = VillageGoals.Instance.ActiveGoals;
            if (goals.Count == 0)
            {
                goalsText.text = "No active goals";
                return;
            }

            var sb = new StringBuilder();
            foreach (var goal in goals)
            {
                string tag = goal.priority switch
                {
                    GoalPriority.Critical => "[!] ",
                    GoalPriority.High     => "[H] ",
                    _                    => "[ ] "
                };
                sb.AppendLine($"{tag}{goal.description}");
            }

            goalsText.text = sb.ToString().TrimEnd();
        }
    }
}
