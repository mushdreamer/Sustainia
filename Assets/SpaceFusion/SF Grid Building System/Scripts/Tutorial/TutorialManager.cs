using System.Collections;
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
    public MonoBehaviour externalCameraController;

    private int _currentStepIndex = 0;
    private bool _isTutorialActive = false;
    private bool _isWaitingForStartCondition = false;
    private Coroutine _cameraCoroutine;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (!enableTutorial) { if (tutorialUI) tutorialUI.Hide(); return; }
        if (tutorialUI) tutorialUI.Initialize(this);
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced += CheckBuildingProgress;
            PlacementSystem.Instance.OnBuildingRemoved += CheckRemovalProgress;
        }
        StartTutorial();
    }

    private void Update()
    {
        if (Debug.isDebugBuild || Application.isEditor)
            if (Input.GetKeyDown(KeyCode.N)) SkipCurrentStep();

        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        if (_isWaitingForStartCondition)
        {
            bool conditionMet = false;
            switch (currentStep.startCondition)
            {
                case TutorialStep.StartCondition.WaitForElectricityDeficit:
                    if (ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < -0.1f) conditionMet = true;
                    break;
                case TutorialStep.StartCondition.Immediate:
                default: conditionMet = true; break;
            }
            if (conditionMet) ActivateStepUI(currentStep);
        }
        else
        {
            bool stepComplete = false;
            if (currentStep.requirePositiveEnergyBalance && ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance >= 0f) stepComplete = true;
            if (currentStep.requireOptimizationGoal && LevelScenarioLoader.Instance != null && LevelScenarioLoader.Instance.IsOptimizationGoalMet()) stepComplete = true;
            if (stepComplete) NextStep();
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
        if (index >= steps.Count) { CompleteTutorial(); return; }
        if (index == 0) TutorialLevelPreparer.Instance.PrepareLayoutForEvent(1);
        else if (index == 4) TutorialLevelPreparer.Instance.PrepareLayoutForEvent(2);

        TutorialStep step = steps[index];
        if (step.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
        {
            _isWaitingForStartCondition = true;
            tutorialUI.Hide();
            if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        }
        else { ActivateStepUI(step); }
    }

    private void ActivateStepUI(TutorialStep step)
    {
        _isWaitingForStartCondition = false;
        tutorialUI.ShowStep(step);

        // --- 核心修改：使用 PCG 圆环作为指示器 ---
        if (step.focusTarget != null)
            UpdateZoneHighlights(step.focusTarget, step.indicatorColor, step.showIndicator);

        if (step.focusTarget != null)
        {
            if (_cameraCoroutine != null) StopCoroutine(_cameraCoroutine);
            _cameraCoroutine = StartCoroutine(MoveCameraSmoothly(step));
        }

        if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = step.shouldPauseGame;
    }

    private void UpdateZoneHighlights(GameObject target, Color color, bool show)
    {
        if (MultiZoneCityGenerator.Instance == null) return;
        foreach (var zone in MultiZoneCityGenerator.Instance.zones)
        {
            if (show && zone.originPoint.gameObject == target)
            {
                zone.isTutorialHighlight = true;
                zone.customHighlightColor = color;
            }
            else { zone.isTutorialHighlight = false; }
        }
    }

    private IEnumerator MoveCameraSmoothly(TutorialStep step)
    {
        Camera mainCam = Camera.main;
        var controller = externalCameraController as SpaceFusion.SF_Grid_Building_System.Scripts.Core.CameraController;
        if (controller != null) controller.enabled = false;

        Vector3 targetPos = step.focusTarget.transform.position;
        float rad = step.cameraAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(0, Mathf.Sin(rad), -Mathf.Cos(rad)) * step.cameraDistance;
        Vector3 finalCamPos = targetPos + offset;

        float elapsed = 0;
        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;
        Quaternion finalRot = Quaternion.LookRotation(targetPos - finalCamPos);

        while (elapsed < 1.0f)
        {
            mainCam.transform.position = Vector3.Lerp(startPos, finalCamPos, elapsed);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, finalRot, elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- 核心修改：同步坐标 ---
        if (controller != null)
        {
            controller.FocusOnPosition(targetPos, step.cameraDistance, step.cameraAngle, mainCam.transform.eulerAngles.y);
            controller.enabled = true;
        }
    }

    public void NextStep() { _currentStepIndex++; PrepareStep(_currentStepIndex); }
    public void SkipCurrentStep() { if (_isTutorialActive) NextStep(); }
    private void CheckBuildingProgress(Placeable d) { /* 逻辑保持不变 */ }
    private void CheckRemovalProgress() { /* 逻辑保持不变 */ }
    private void CompleteTutorial() { _isTutorialActive = false; tutorialUI.Hide(); if (externalCameraController) externalCameraController.enabled = true; }
}