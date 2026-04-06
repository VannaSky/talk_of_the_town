using TMPro;
using UnityEngine;

namespace MainMenu
{
    public class LLMChooser : MonoBehaviour
    {

        [SerializeField]
        private GlobalSettings globalSettings;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
            var dropDown = GetComponent<TMP_Dropdown>();
            dropDown.onValueChanged.AddListener(delegate { globalSettings.LLMModel = dropDown.options[dropDown.value].text; });
        
            globalSettings.LLMModel = dropDown.options[dropDown.value].text;
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
