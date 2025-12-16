using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables; // 引用 Placeable
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

        // --- 修改点：订阅 OnBuildingPlaced 事件以进行严格检查 ---
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced += CheckBuildingProgress;
        }

        StartTutorial();
    }

    private void OnDestroy()
    {
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced -= CheckBuildingProgress;
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

        // --- 修改点：只要教程步骤显示，就暂停游戏模拟 ---
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.isPaused = true;
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

    // --- 修改点：接收 Placeable 参数，进行类型检查 ---
    private void CheckBuildingProgress(Placeable placedData)
    {
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        // 1. 如果当前步骤不需要建造，直接忽略
        if (!currentStep.requireBuilding) return;

        // 2. --- 核心修改：严格检查建筑类型 ---
        if (placedData != null && placedData.Prefab != null)
        {
            BuildingEffect effect = placedData.Prefab.GetComponent<BuildingEffect>();
            if (effect != null)
            {
                // 如果建造的类型不匹配目标类型，直接返回，不进行下一步
                if (effect.type != currentStep.targetBuildingType)
                {
                    Debug.Log($"教程：建造了错误的建筑类型 {effect.type}，目标是 {currentStep.targetBuildingType}");
                    return;
                }
            }
        }

        // 3. 如果类型匹配，延迟一小会儿跳到下一步
        Invoke(nameof(NextStep), 0.5f);
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false;
        tutorialUI.Hide();

        // --- 修改点：教程完成，恢复游戏模拟 ---
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.isPaused = false;
        }

        Debug.Log("Tutorial Completed!");
        ResourceManager.Instance.AddMoney(500);
    }
}