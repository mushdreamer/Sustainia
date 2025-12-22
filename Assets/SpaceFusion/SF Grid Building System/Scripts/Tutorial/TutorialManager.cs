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

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.isPaused = true;
        }

        TutorialStep step = steps[_currentStepIndex];
        tutorialUI.ShowStep(step);
    }

    public void NextStep()
    {
        _currentStepIndex++;
        ShowCurrentStep();
    }

    private void CheckBuildingProgress(Placeable placedData)
    {
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

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
            // 严格检查类型
            if (effect != null && effect.type == currentStep.targetBuildingType)
            {
                Invoke(nameof(NextStep), 0.5f);
            }
        }
    }

    private void CheckRemovalProgress()
    {
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

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