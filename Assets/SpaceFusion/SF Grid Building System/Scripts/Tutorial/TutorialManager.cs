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
                    // 只有当电力确实小于0时才触发
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
            // 检查电力是否回正
            if (currentStep.requirePositiveEnergyBalance)
            {
                if (ResourceManager.Instance != null)
                {
                    // 只要电力 >= 0 就算通过
                    if (ResourceManager.Instance.ElectricityBalance >= 0f)
                    {
                        Debug.Log("[Tutorial] 电力平衡已恢复，步骤完成！");
                        NextStep();
                    }
                }
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

            // 等待期间，强制允许游戏运行，否则玩家没法把电用超
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

        // --- 核心修改：根据配置决定是否暂停游戏 ---
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.isPaused = step.shouldPauseGame;

            // 为了防止UI不刷新，我们在进入步骤时强制刷新一次UI（可选）
            // ResourceManager.Instance.UpdateUI(); 
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