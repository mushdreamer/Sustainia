using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("Configuration")]
    public bool enableTutorial = true;
    public List<TutorialStep> steps;

    [Header("References")]
    public TutorialUI tutorialUI;

    private int _currentStepIndex = 0;
    private bool _isTutorialActive = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); }
        Instance = this;
    }

    private void Start()
    {
        if (!enableTutorial)
        {
            tutorialUI.Hide();
            return;
        }

        tutorialUI.Initialize(this);

        // 订阅 PlacementSystem 的事件
        // 当放置状态结束（意味着玩家可能放置了东西）时检查
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnPlacementStateEnd += CheckBuildingProgress;
        }

        StartTutorial();
    }

    private void OnDestroy()
    {
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnPlacementStateEnd -= CheckBuildingProgress;
        }
    }

    public void StartTutorial()
    {
        _isTutorialActive = true;
        _currentStepIndex = 0;
        ShowCurrentStep();
    }

    private void ShowCurrentStep()
    {
        if (_currentStepIndex >= steps.Count)
        {
            CompleteTutorial();
            return;
        }

        TutorialStep step = steps[_currentStepIndex];
        tutorialUI.ShowStep(step);

        Debug.Log($"Tutorial Step: {step.stepName}");
    }

    public void NextStep()
    {
        _currentStepIndex++;
        ShowCurrentStep();
    }

    // 当玩家完成一次放置操作后，系统会自动调用这个检查
    private void CheckBuildingProgress()
    {
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        // 如果当前步骤不需要建造，忽略
        if (!currentStep.requireBuilding) return;

        // 检查玩家刚刚造了什么
        // 这里我们通过检查场景中最后生成的物体，或者简单检查 ResourceManager 中的计数
        // 更严谨的方法是 PlacementSystem 发事件告诉我们要造了什么，这里用简单方法：
        // 检查特定类型的建筑数量是否 > 0 (假设教程开始时是0)

        if (ResourceManager.Instance.CanBuildBuilding(currentStep.targetBuildingType) == false)
        {
            // ResourceManager.CanBuildBuilding 返回 false 意味着已经有这个建筑了 (基于你的逻辑)
            // 或者我们可以直接查数量
            // 简单起见，只要玩家完成了放置动作，我们就假设他造对了 (在早期教程中通常只有一个选项)
            // 更好的方式是检查 ResourceManager._buildingCounts

            // 延迟一点点跳过，给玩家看一眼建造效果
            Invoke(nameof(NextStep), 0.5f);
        }
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false;
        tutorialUI.Hide();
        Debug.Log("Tutorial Completed!");

        // 可以在这里给玩家发一笔奖金
        ResourceManager.Instance.AddMoney(500);
    }
}