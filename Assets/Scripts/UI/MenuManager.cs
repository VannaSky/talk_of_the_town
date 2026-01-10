using System;
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
        
        [SerializeField] private string model;
        [SerializeField] private string reasoning;

        [SerializeField] private TextMeshProUGUI woodText;
        [SerializeField] private TextMeshProUGUI stoneText;
        [SerializeField] private TextMeshProUGUI seedText;
        [SerializeField] private TextMeshProUGUI foodText;
        [SerializeField] private TextMeshProUGUI modelText;
       
        private VillageState _villageState;
        private LLMController _llmController;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _villageState = VillageState.Instance;
            _llmController = LLMController.Instance;
        }

        // Update is called once per frame
        void Update()
        {
            wood = _villageState.Wood;
            stone = _villageState.Stone;
            seed = _villageState.Seeds;
            food = _villageState.Food;
            
            model = _llmController.CurrentModel;
        
            woodText.text = wood.ToString();
            stoneText.text = stone.ToString();
            seedText.text = seed.ToString();
            foodText.text = food.ToString();
            modelText.text = model;
            
        }
    }
}
