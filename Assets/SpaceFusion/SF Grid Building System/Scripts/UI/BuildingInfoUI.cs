using System.Text;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class BuildingInfoUI : MonoBehaviour
    {
        public static BuildingInfoUI Instance;

        [Header("UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Position Tuning")]
        [Range(0f, 1f)][SerializeField] private float horizontalPercent = 0.95f;
        [Range(0f, 1f)][SerializeField] private float verticalPercent = 0.5f;
        [SerializeField] private Vector2 pixelOffset = new Vector2(0, 0);

        private RectTransform _rectTransform;
        private TutorialBuildingEffect _currentTutorialBuilding;
        private string _cachedName;
        private string _cachedDetails;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _rectTransform = GetComponent<RectTransform>();
            Hide();
        }

        private void Update()
        {
            if (panel.activeSelf)
            {
                RefreshDisplay();
            }
        }

        public void Show(string name, string details)
        {
            _cachedName = name;
            _cachedDetails = details;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 尝试从自身或父级获取 TutorialBuildingEffect
                _currentTutorialBuilding = hit.collider.GetComponentInParent<TutorialBuildingEffect>();
            }

            RefreshDisplay();
            SetPanelPosition();
            panel.SetActive(true);
        }

        private void RefreshDisplay()
        {
            nameText.text = _cachedName;

            if (_currentTutorialBuilding != null)
            {
                StringBuilder sb = new StringBuilder();

                if (_currentTutorialBuilding.tutorialType == TutorialBuildingType.Battery)
                {
                    // 获取该建筑生效的阈值（可能是自定义的，也可能是全局默认的）
                    float threshold = _currentTutorialBuilding.GetEffectiveThreshold();
                    float currentDemand = ResourceManager.Instance != null ? ResourceManager.Instance.CurrentTotalDemand : 0f;

                    // 修改判定逻辑：如果当前需求超过了玩家定义的阈值
                    if (currentDemand > threshold)
                    {
                        sb.AppendLine("<color=red><b>Status: OVERLOADED</b></color>");
                        sb.AppendLine($"<color=red>Demand ({currentDemand:F1}) > Threshold ({threshold:F1})</color>");
                        sb.Append("<color=red>Grid capacity exceeded.</color>");
                    }
                    else
                    {
                        sb.AppendLine("<color=green>Status: NORMAL</color>");
                        sb.Append($"<size=80%>Demand: {currentDemand:F1} / Threshold: {threshold:F1}</size>");
                    }
                }
                else if (_currentTutorialBuilding.tutorialType == TutorialBuildingType.LocalGen)
                {
                    float genValue = -_currentTutorialBuilding.electricityChange;
                    float co2Value = _currentTutorialBuilding.co2Change;
                    sb.AppendLine($"Electricity Supply: {genValue:F1} units");
                    sb.Append($"CO2 Emission: {co2Value:F1} kg/day");
                }
                else
                {
                    sb.Append(_cachedDetails);
                }

                statsText.text = sb.ToString();
            }
            else
            {
                statsText.text = _cachedDetails;
            }
        }

        private void SetPanelPosition()
        {
            if (_rectTransform == null) return;
            float targetX = Screen.width * horizontalPercent;
            float targetY = Screen.height * verticalPercent;
            _rectTransform.position = new Vector3(targetX, targetY, 0) + (Vector3)pixelOffset;
        }

        public void Hide()
        {
            _currentTutorialBuilding = null;
            panel.SetActive(false);
        }
    }
}