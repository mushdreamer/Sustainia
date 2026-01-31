using System.Text;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using TMPro;
using UnityEngine;

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

        [Header("Position Tuning (微调设置)")]
        [Tooltip("控制UI在屏幕右侧的百分比位置，1.0代表最右边")]
        [Range(0f, 1f)]
        [SerializeField] private float horizontalPercent = 0.95f;

        [Tooltip("控制UI在垂直方向的百分比位置，0.5为居中")]
        [Range(0f, 1f)]
        [SerializeField] private float verticalPercent = 0.5f;

        [Tooltip("在百分比位置基础上的像素偏移")]
        [SerializeField] private Vector2 pixelOffset = new Vector2(0, 0);

        private RectTransform _rectTransform;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            _rectTransform = GetComponent<RectTransform>();
            Hide();
        }

        // 删除了原有的 Update 鼠标跟随逻辑，以解决 UI 与建筑重合的问题
        private void Update()
        {
            // 如果你想在运行时拖动滑杆实时看效果，可以把下面的定位函数放在这里
            // SetPanelPosition(); 
        }

        public void Show(string name, string details)
        {
            nameText.text = name;
            statsText.text = details;

            // 在显示时计算位置
            SetPanelPosition();

            panel.SetActive(true);
        }

        private void SetPanelPosition()
        {
            if (_rectTransform == null) return;

            // 基于当前屏幕分辨率计算坐标
            float targetX = Screen.width * horizontalPercent;
            float targetY = Screen.height * verticalPercent;

            // 应用计算出的位置加上你的微调偏移
            transform.position = new Vector3(targetX, targetY, 0) + (Vector3)pixelOffset;
        }

        public void Hide()
        {
            panel.SetActive(false);
        }
    }
}