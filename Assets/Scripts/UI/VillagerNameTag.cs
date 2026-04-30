using TMPro;
using UnityEngine;

namespace UI
{
    public class VillagerNameTag : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;

        private Camera _camera;

        private void Start()
        {
            _camera = Camera.main;

            var villager = GetComponentInParent<Villager>();
            if (villager != null)
                nameText.text = villager.villagerName;
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            Vector3 dir = transform.position - _camera.transform.position;
            dir.y = 0f;

            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
