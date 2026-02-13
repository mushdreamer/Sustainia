using UnityEngine;
using TMPro;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public class FormulaUI : MonoBehaviour
    {
        public static FormulaUI Instance;

        [Header("UI References")]
        public GameObject panel;
        public TextMeshProUGUI formulaText;

        private TutorialStep _activeStep;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            Hide();
        }

        private void Update()
        {
            if (panel != null && panel.activeSelf && _activeStep != null)
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
            else { Hide(); }
        }

        private void RefreshDisplay()
        {
            if (_activeStep == null || ResourceManager.Instance == null || formulaText == null) return;

            string content = _activeStep.formulaContent;
            if (string.IsNullOrEmpty(content)) return;

            // 处理 S 公式
            if (content.Contains("{s}"))
            {
                content = content.Replace("{s}", ResourceManager.Instance.ProsperityScoreS.ToString("F1"));
                content = content.Replace("{w1}", ResourceManager.Instance.w1.ToString("F1"));
                content = content.Replace("{w2}", ResourceManager.Instance.w2.ToString("F1"));
                content = content.Replace("{gold}", ResourceManager.Instance.CurrentGoldOutput.ToString("F0"));
                content = content.Replace("{green}", ResourceManager.Instance.CurrentGreenScore.ToString("F1"));
            }

            // 处理 P 公式
            if (content.Contains("{p}") || content.Contains("{val}"))
            {
                float houseVal = ResourceManager.Instance.GetTotalBuildingCount("House") * 10f;
                float fgap = Mathf.Abs(Mathf.Min(0, ResourceManager.Instance.FoodBalance));
                float egap = Mathf.Abs(Mathf.Min(0, ResourceManager.Instance.ElectricityBalance));
                float totalGap = (fgap + egap) * 2.0f;
                float p = houseVal - totalGap;

                content = content.Replace("{val}", houseVal.ToString("F0"));
                content = content.Replace("{fgap}", fgap.ToString("F1"));
                content = content.Replace("{egap}", egap.ToString("F1"));
                content = content.Replace("{gap}", totalGap.ToString("F1"));
                content = content.Replace("{p}", p.ToString("F1"));
                content = content.Replace("{-p}", (-p).ToString("F1"));
            }

            float food = ResourceManager.Instance.FoodBalance;
            float elec = ResourceManager.Instance.ElectricityBalance;
            float co2 = ResourceManager.Instance.GetCurrentNetEmission();

            content = content.Replace("{food}", (food >= 0 ? "+" : "") + food.ToString("F1"));
            content = content.Replace("{elec}", (elec >= 0 ? "+" : "") + elec.ToString("F1"));
            content = content.Replace("{co2}", co2.ToString("F1"));

            if (LevelScenarioLoader.Instance != null && LevelScenarioLoader.Instance.currentLevel != null)
                content = content.Replace("{targetCo2}", LevelScenarioLoader.Instance.currentLevel.goalCo2.ToString("F1"));

            formulaText.text = content;
        }

        // 修复报错：补回这个被我弄丢的方法
        public void UpdateFormulaText(string text)
        {
            if (formulaText != null)
            {
                formulaText.text = text;
            }
        }

        public void Hide() { _activeStep = null; if (panel != null) panel.SetActive(false); }
    }
}