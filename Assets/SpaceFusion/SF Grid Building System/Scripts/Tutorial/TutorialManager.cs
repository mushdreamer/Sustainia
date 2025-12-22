using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;
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
    private bool _isWaitingForStartCondition = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (!enableTutorial)
        {
            if (tutorialUI) tutorialUI.Hide();
            return;
        }

        if (tutorialUI) tutorialUI.Initialize(this);

        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced += CheckBuildingProgress;
            PlacementSystem.Instance.OnBuildingRemoved += CheckRemovalProgress;
        }

        StartTutorial();
    }

    private void OnDestroy()
    {
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced -= CheckBuildingProgress;
            PlacementSystem.Instance.OnBuildingRemoved -= CheckRemovalProgress;
        }
    }

    private void Update()
    {
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        // --- 逻辑 A: 检查“触发条件” ---
        if (_isWaitingForStartCondition)
        {
            bool conditionMet = false;

            switch (currentStep.startCondition)
            {
                case TutorialStep.StartCondition.WaitForElectricityDeficit:
                    if (ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < -0.1f)
                    {
                        conditionMet = true;
                    }
                    break;

                case TutorialStep.StartCondition.Immediate:
                default:
                    conditionMet = true;
                    break;
            }

            if (conditionMet)
            {
                ActivateStepUI(currentStep);
            }
        }

        // --- 逻辑 B: 检查“完成条件” ---
        else
        {
            bool stepComplete = false;

            // 1. 检查电力是否回正
            if (currentStep.requirePositiveEnergyBalance)
            {
                if (ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance >= 0f)
                {
                    Debug.Log("[Tutorial] 电力平衡已恢复，步骤完成！");
                    stepComplete = true;
                }
            }

            // 2. 新增：检查是否达到 Mathematical Optimization 目标
            if (currentStep.requireOptimizationGoal)
            {
                if (LevelScenarioLoader.Instance != null && LevelScenarioLoader.Instance.IsOptimizationGoalMet())
                {
                    Debug.Log("[Tutorial] 优化目标已达成 (Optimization Goal Met)！");
                    stepComplete = true;
                }
            }

            if (stepComplete)
            {
                NextStep();
            }
        }
    }

    public void StartTutorial()
    {
        _isTutorialActive = true;
        _currentStepIndex = 0;
        PrepareStep(_currentStepIndex);
    }

    private void PrepareStep(int index)
    {
        if (index >= steps.Count)
        {
            CompleteTutorial();
            return;
        }

        TutorialStep step = steps[index];

        if (step.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
        {
            Debug.Log($"[Tutorial] Step {index + 1} 等待电力赤字...");
            _isWaitingForStartCondition = true;
            tutorialUI.Hide();
            if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        }
        else
        {
            ActivateStepUI(step);
        }
    }

    private void ActivateStepUI(TutorialStep step)
    {
        _isWaitingForStartCondition = false;
        tutorialUI.ShowStep(step);

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.isPaused = step.shouldPauseGame;
            // 如果是检查优化目标，通常需要游戏运行起来让数值变化，或者至少允许操作
            if (step.requireOptimizationGoal)
            {
                // 保持暂停可能无法更新数值，具体视 ResourceManager 逻辑而定
                // 如果 ResourceManager 的数值是实时计算的（非 Tick），则可以暂停
                // 这里为了保险，如果是优化任务，建议不要完全暂停，或者只暂停时间流逝但不暂停数值计算
            }
        }
    }

    public void NextStep()
    {
        _currentStepIndex++;
        PrepareStep(_currentStepIndex);
    }

    private void CheckBuildingProgress(Placeable placedData)
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];
        if (!currentStep.requireBuilding) return;

        if (currentStep.allowAnyBuilding)
        {
            Invoke(nameof(NextStep), 0.5f);
            return;
        }

        if (placedData != null && placedData.Prefab != null)
        {
            BuildingEffect effect = placedData.Prefab.GetComponent<BuildingEffect>();
            if (effect != null && effect.type == currentStep.targetBuildingType)
            {
                Invoke(nameof(NextStep), 0.5f);
            }
        }
    }

    private void CheckRemovalProgress()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;
        TutorialStep currentStep = steps[_currentStepIndex];

        if (currentStep.requireRemoval)
        {
            Invoke(nameof(NextStep), 0.5f);
        }
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false;
        tutorialUI.Hide();
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.isPaused = false;
        }
        Debug.Log("Tutorial Completed!");
    }
}