using UnityEngine;
using TMPro;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public class FormulaUI : MonoBehaviour
    {
        public static FormulaUI Instance;

        [Header("UI References")]
        public GameObject panel;            // 公式面板的根物体
        public TextMeshProUGUI formulaText; // 用于显示公式的文本组件

        private TutorialStep _activeStep;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            Hide();
        }

        private void Update()
        {
            // 如果面板激活且有步骤数据，则实时更新数值
            if (panel.activeSelf && _activeStep != null)
            {
                RefreshDisplay();
            }
        }

        public void SetupStep(TutorialStep step)
        {
            _activeStep = step;
            if (step != null && step.showFormulaPanel)
            {
                panel.SetActive(true);
                RefreshDisplay();
            }
            else
            {
                Hide();
            }
        }

        private void RefreshDisplay()
        {
            if (_activeStep == null || ResourceManager.Instance == null) return;

            string content = _activeStep.formulaContent;

            // --- 保留你原有的资源数据逻辑 ---
            float elec = ResourceManager.Instance.ElectricityBalance;
            float co2 = ResourceManager.Instance.GetCurrentNetEmission();
            float food = ResourceManager.Instance.FoodBalance;

            content = content.Replace("{elec}", (elec >= 0 ? "+" : "") + elec.ToString("F1"));
            content = content.Replace("{co2}", co2.ToString("F1"));
            content = content.Replace("{food}", (food >= 0 ? "+" : "") + food.ToString("F1"));

            if (LevelScenarioLoader.Instance != null && LevelScenarioLoader.Instance.currentLevel != null)
            {
                content = content.Replace("{targetCo2}", LevelScenarioLoader.Instance.currentLevel.goalCo2.ToString("F1"));
            }

            // --- 严谨新增：针对 Event 5 的 Prosperity (P) 逻辑 ---
            // 仅在字符串包含相关占位符时计算，不干扰其他步骤
            if (content.Contains("{p}") || content.Contains("{val}") || content.Contains("{gap}"))
            {
                float val = ResourceManager.Instance.GetTotalBuildingCount("House") * 10f;
                float fgap = Mathf.Abs(Mathf.Min(0, food));
                float egap = Mathf.Abs(Mathf.Min(0, elec));
                float totalGap = (fgap + egap) * 2.0f; // 惩罚权重
                float p = val - totalGap;

                content = content.Replace("{val}", val.ToString("F0"));
                content = content.Replace("{fgap}", fgap.ToString("F1"));
                content = content.Replace("{egap}", egap.ToString("F1"));
                content = content.Replace("{gap}", totalGap.ToString("F1"));
                content = content.Replace("{p}", p.ToString("F1"));
                content = content.Replace("{-p}", (-p).ToString("F1"));
            }

            formulaText.text = content;
        }

        public void Hide()
        {
            _activeStep = null;
            if (panel != null) panel.SetActive(false);
        }

        public void UpdateFormulaText(string text)
        {
            if (formulaText != null)
            {
                formulaText.text = text;
            }
        }
    }
}