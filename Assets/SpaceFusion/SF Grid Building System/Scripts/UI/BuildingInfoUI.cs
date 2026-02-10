using System.Text;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems; // 用于射线检测判断

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
            // 如果面板是打开的，我们需要实时刷新内容
            if (panel.activeSelf)
            {
                RefreshDisplay();
            }
        }

        // 修改后的通用 Show 方法：增加自动识别逻辑
        public void Show(string name, string details)
        {
            _cachedName = name;
            _cachedDetails = details;

            // 核心修复：直接通过物理射线探测鼠标下的物体
            // 这样无论外部脚本怎么传，UI 都会根据物理事实进行判断
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

            // 如果探测到是教学建筑，执行定制逻辑
            if (_currentTutorialBuilding != null)
            {
                StringBuilder sb = new StringBuilder();

                if (_currentTutorialBuilding.tutorialType == TutorialBuildingType.Battery)
                {
                    // 只有 Battery 显示过载状态
                    if (ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < 0)
                    {
                        sb.AppendLine("<color=red><b>状态: 过载 (OVERLOADED)</b></color>");
                        sb.Append("<color=red>电网电力不足以支撑电池运作</color>");
                    }
                    else
                    {
                        sb.Append("<color=green>状态: 正常 (NORMAL)</color>");
                    }
                }
                else if (_currentTutorialBuilding.tutorialType == TutorialBuildingType.LocalGen)
                {
                    // LocalGen 显示电力和 CO2
                    float genValue = -_currentTutorialBuilding.electricityChange;
                    float co2Value = _currentTutorialBuilding.co2Change;
                    sb.AppendLine($"电力供应: {genValue:F1} units");
                    sb.Append($"碳排放量: {co2Value:F1} kg/day");
                }
                else
                {
                    // 其他教学建筑使用默认信息
                    sb.Append(_cachedDetails);
                }

                statsText.text = sb.ToString();
            }
            else
            {
                // 普通建筑，直接显示外部传进来的 details
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