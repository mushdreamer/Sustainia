using TMPro;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    /// <summary>
    /// 管理商店界面的鼠标悬停提示框
    /// </summary>
    public class ShopTooltipUI : MonoBehaviour
    {
        public static ShopTooltipUI Instance;

        [SerializeField]
        private GameObject tooltipPanel;

        [SerializeField]
        private TextMeshProUGUI nameText;

        [Header("Settings")]
        [SerializeField]
        private Vector2 offset = new Vector2(15, -15); // 提示框相对于鼠标的偏移量

        private RectTransform _rectTransform;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _rectTransform = tooltipPanel.GetComponent<RectTransform>();
            Hide(); // 默认隐藏
        }

        private void Update()
        {
            if (tooltipPanel.activeSelf)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            // 让提示框跟随鼠标位置
            Vector2 mousePos = Input.mousePosition;

            // 这里假设 Canvas 是 Screen Space - Overlay 模式
            // 如果是 Camera 模式，逻辑可能需要微调，但在大多数 UI 场景下直接赋值即可
            transform.position = mousePos + offset;

            // (进阶优化：可以在这里添加边界检测逻辑，防止提示框跑出屏幕，目前暂略)
        }

        public void Show(string buildingName)
        {
            nameText.text = buildingName;
            tooltipPanel.SetActive(true);
            UpdatePosition(); // 显示瞬间立刻更新一次位置，防止闪烁
        }

        public void Hide()
        {
            tooltipPanel.SetActive(false);
        }
    }
}