using TMPro;
using UnityEngine;

namespace UI
{
    public class VillagerNameTag : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private bool showEnergy = true;

        private Camera _camera;
        private Villager _villager;

        private void Start()
        {
            _camera = Camera.main;

            _villager = GetComponentInParent<Villager>();
            if (_villager != null)
                nameText.text = _villager.villagerName;
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            Vector3 dir = transform.position - _camera.transform.position;
            dir.y = 0f;

            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);

            if (showEnergy && energyText != null && _villager != null)
            {
                int e = _villager.EnergyPercent;
                string hex = e < 5 ? "FF4444" : e < 30 ? "FFCC00" : "44FF44";
                energyText.text = $"<sprite=0> <color=#{hex}>{e}%</color>";
            }
        }
    }
}
