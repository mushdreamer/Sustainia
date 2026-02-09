using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public class TutorialUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject panel; // 整个教程面板
        public TextMeshProUGUI instructionText;
        public Button nextButton; // "下一步" 按钮

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

            // --- 关键修改点：更新自动化条件的定义 ---
            // 只要勾选了任何一个自动化判定项，hasAutomation 就会为 true
            bool hasAutomation =
                step.requireBuilding ||
                step.requireTutorialBuilding ||
                step.requireRemoval ||
                step.requirePositiveEnergyBalance ||
                step.requireOptimizationGoal ||
                // 新增的 6 个状态检查：
                step.requireFoodSatisfied ||
                step.requireFoodShortage ||
                step.requireElecStable ||
                step.requireElecDeficit ||
                step.requireCo2WithinLimit ||
                step.requireCo2OverLimit;

            // 逻辑修正：
            // 1. 如果显式勾选了 requireInput，则显示按钮（允许玩家手动跳过或作为必要确认）。
            // 2. 如果没有任何自动化条件 (!hasAutomation)，说明这只是一个纯展示步骤，必须显示按钮让玩家继续。
            // 3. 只有当存在自动化条件且未勾选 requireInput 时，按钮才会隐藏，等待逻辑自动触发跳转。
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(step.requireInput || !hasAutomation);
            }
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnNextClicked()
        {
            if (_manager != null) _manager.NextStep();
        }
    }
}