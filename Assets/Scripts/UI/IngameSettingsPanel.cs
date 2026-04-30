using TMPro;
using UnityEngine;

namespace UI
{
    public class IngameSettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private TextMeshProUGUI toggleButtonText;

        private bool _isVisible = true;

        public void Toggle()
        {
            _isVisible = !_isVisible;
            content.SetActive(_isVisible);
            toggleButtonText.text = _isVisible ? "Hide" : "Show";
        }
    }
}
