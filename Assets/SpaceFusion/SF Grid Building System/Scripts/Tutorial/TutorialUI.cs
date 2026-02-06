using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
        nextButton.onClick.AddListener(OnNextClicked);
    }

    public void ShowStep(TutorialStep step)
    {
        panel.SetActive(true);
        instructionText.text = step.instructionText;

        // 修改逻辑：
        // 1. 如果显式勾选了 requireInput，则一定显示按钮（作为兜底）。
        // 2. 如果没有任何自动化条件（建造、删除、能源、目标），那它就是一个纯文本阅读步骤，也必须显示按钮。
        bool hasNoAutomation = !step.requireBuilding && !step.requireTutorialBuilding &&
                               !step.requireRemoval && !step.requirePositiveEnergyBalance &&
                               !step.requireOptimizationGoal;

        nextButton.gameObject.SetActive(step.requireInput || hasNoAutomation);
    }

    public void Hide()
    {
        panel.SetActive(false);
    }

    private void OnNextClicked()
    {
        _manager.NextStep();
    }
}