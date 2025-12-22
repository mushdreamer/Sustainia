using System.Text;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
using TMPro;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.UI
{
    public class BuildingInfoUI : MonoBehaviour
    {
        public static BuildingInfoUI Instance;

        [Header("UI References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statsText; // 用于显示输入输出详情

        [Header("Settings")]
        [SerializeField] private Vector2 offset = new Vector2(20, -20);

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            Hide();
        }

        private void Update()
        {
            if (panel.activeSelf)
            {
                // 跟随鼠标
                transform.position = (Vector2)Input.mousePosition + offset;
            }
        }

        public void Show(string name, string details)
        {
            nameText.text = name;
            statsText.text = details;
            panel.SetActive(true);
        }

        public void Hide()
        {
            panel.SetActive(false);
        }
    }
}