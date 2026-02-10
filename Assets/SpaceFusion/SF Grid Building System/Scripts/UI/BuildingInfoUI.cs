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
            // 每帧更新显示内容，以实时反映电力平衡变化导致的过载状态
            if (panel.activeSelf)
            {
                RefreshDisplay();
            }
        }

        // 修改说明：保留了上一版的射线检测识别逻辑，这是确保 UI 正确识别 TutorialBuilding 的关键
        public void Show(string name, string details)
        {
            _cachedName = name;
            _cachedDetails = details;

            // 通过物理射线探测鼠标下的物体，自动识别是否为教学建筑
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
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
                    // 英文显示及颜色同步：与 ResourceManager 逻辑一致，负平衡为红色，正平衡为绿色
                    if (ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < 0)
                    {
                        sb.AppendLine("<color=red><b>Status: OVERLOADED</b></color>");
                        sb.Append("<color=red>Grid power insufficient for battery operation</color>");
                    }
                    else
                    {
                        sb.Append("<color=green>Status: NORMAL</color>");
                    }
                }
                else if (_currentTutorialBuilding.tutorialType == TutorialBuildingType.LocalGen)
                {
                    // LocalGen 显示逻辑：将电力变更数值转换为正数的供应量进行展示
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
                // 普通建筑保持原样显示
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