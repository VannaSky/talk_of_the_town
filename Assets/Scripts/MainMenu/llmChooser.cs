using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MainMenu
{
    public class LLMChooser : MonoBehaviour
    {
        [SerializeField] private GlobalSettings globalSettings;

        private TMP_Dropdown _dropdown;

        void Start()
        {
            _dropdown = GetComponent<TMP_Dropdown>();
            _dropdown.onValueChanged.AddListener(OnDropdownChanged);
            StartCoroutine(FetchAndPopulate());
        }

        private IEnumerator FetchAndPopulate()
        {
            var task = ollama.Ollama.List();
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted || task.Result == null || task.Result.Length == 0)
            {
                Debug.LogWarning("[LLMChooser] Could not fetch model list from Ollama — keeping static options.");
                yield break;
            }

            _dropdown.ClearOptions();

            var options = new List<TMP_Dropdown.OptionData>();
            int selectedIndex = 0;

            for (int i = 0; i < task.Result.Length; i++)
            {
                string name = task.Result[i].name;
                options.Add(new TMP_Dropdown.OptionData(name));
                if (name == globalSettings.LLMModel)
                    selectedIndex = i;
            }

            _dropdown.AddOptions(options);
            _dropdown.SetValueWithoutNotify(selectedIndex);
            _dropdown.RefreshShownValue();

            if (options.Count > 0)
                globalSettings.LLMModel = options[selectedIndex].text;
        }

        private void OnDropdownChanged(int index)
        {
            if (_dropdown.options.Count > index)
                globalSettings.LLMModel = _dropdown.options[index].text;
        }
    }
}
