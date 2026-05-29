using System.Text;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// In-game HUD display for researcher-set Global Goals.
    /// Shows each goal with live progress and a completion marker.
    /// Attach to a GameObject that has (or is a child of) a TMP_Text component.
    /// </summary>
    public class GlobalGoalsDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text goalsText;

        void Awake()
        {
            if (goalsText == null)
                goalsText = GetComponent<TMP_Text>();
        }

        void Update()
        {
            if (goalsText == null || GlobalGoals.Instance == null) return;

            var goals = GlobalGoals.Instance.Goals;
            if (goals.Count == 0)
            {
                goalsText.text = "";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<b>RESEARCHER GOALS</b>");
            foreach (var goal in goals)
            {
                string check    = goal.isCompleted ? "[+]" : "[ ]";
                string progress = GetProgress(goal);
                sb.AppendLine($"{check} {goal.Description} {progress}");
            }

            goalsText.text = sb.ToString().TrimEnd();
        }

        private static string GetProgress(GlobalGoal goal)
        {
            if (VillageState.Instance == null) return "";

            switch (goal.type)
            {
                case GlobalGoalType.ResourceAmount:
                    int res = VillageState.Instance.GetResource(goal.targetResource);
                    return $"({res}/{goal.targetAmount})";

                case GlobalGoalType.BuildingCount:
                    int built = CountAllCompletedBuildings();
                    return $"({built}/{goal.targetAmount})";

                case GlobalGoalType.PopulationCount:
                    return $"({VillageState.Instance.Villagers.Count}/{goal.targetAmount})";

                default:
                    return "";
            }
        }

        private static int CountAllCompletedBuildings()
        {
            int count = 0;
            var buildings = Object.FindObjectsByType<Buildings.Building>(FindObjectsSortMode.None);
            foreach (var b in buildings)
            {
                if (b != null && b.IsFinished())
                    count++;
            }
            return count;
        }
    }
}
