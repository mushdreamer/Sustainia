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

        // 如果这一步需要玩家建造东西，就隐藏“下一步”按钮，强制玩家去建造
        // 如果只是阅读文本，则显示“下一步”
        nextButton.gameObject.SetActive(step.requireInput);
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