using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public class TutorialUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panel;
        public TextMeshProUGUI instructionText;
        public Button nextButton;

        private TutorialManager _manager;

        public void Initialize(TutorialManager manager)
        {
            _manager = manager;
            if (nextButton != null)
                nextButton.onClick.AddListener(OnNextClicked);
        }

        public void ShowStep(TutorialStep step)
        {
            panel.SetActive(true);
            instructionText.text = step.instructionText;

            // --- 联动联动：通知 FormulaUI 设置该步骤的公式 ---
            if (FormulaUI.Instance != null)
            {
                FormulaUI.Instance.SetupStep(step);
            }

            bool hasAutomation =
                step.requireBuilding ||
                step.requireTutorialBuilding ||
                step.requireRemoval ||
                step.requirePositiveEnergyBalance ||
                step.requireOptimizationGoal ||
                step.requireFoodSatisfied ||
                step.requireFoodShortage ||
                step.requireElecStable ||
                step.requireElecDeficit ||
                step.requireCo2WithinLimit ||
                step.requireCo2OverLimit;

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(step.requireInput || !hasAutomation);
            }
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);

            // --- 隐藏时也关闭公式面板 ---
            if (FormulaUI.Instance != null)
            {
                FormulaUI.Instance.Hide();
            }
        }

        private void OnNextClicked()
        {
            if (_manager != null) _manager.NextStep();
        }
    }
}