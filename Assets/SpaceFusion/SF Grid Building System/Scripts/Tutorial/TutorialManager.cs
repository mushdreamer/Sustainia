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

    private void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

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
        if (Input.GetKeyDown(KeyCode.N)) SkipCurrentStep();
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        if (_isWaitingForStartCondition)
        {
            bool met = false;
            if (currentStep.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
                met = ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < -0.1f;
            else met = true;

            if (met) ActivateStepUI(currentStep);
        }
        else
        {
            bool complete = false;
            if (currentStep.requirePositiveEnergyBalance && ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance >= 0f) complete = true;
            if (currentStep.requireOptimizationGoal && LevelScenarioLoader.Instance != null && LevelScenarioLoader.Instance.IsOptimizationGoalMet()) complete = true;
            if (complete) NextStep();
        }
    }

    public void StartTutorial() { _isTutorialActive = true; _currentStepIndex = 0; PrepareStep(0); }

    private void PrepareStep(int index)
    {
        if (index >= steps.Count) { CompleteTutorial(); return; }

        TutorialStep step = steps[index];

        if (step.clearSceneBeforeStart && TutorialLevelPreparer.Instance != null)
        {
            TutorialLevelPreparer.Instance.ClearAllBuildings();
            if (step.layoutToLoad >= 0)
            {
                TutorialLevelPreparer.Instance.PrepareLayoutForEvent(step.layoutToLoad);
            }
        }

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
            bool isTarget = zone.originPoint.gameObject == target;
            zone.isTutorialHighlight = show && isTarget;
            zone.customHighlightColor = color;
        }
    }

    private IEnumerator MoveCameraSmoothly(TutorialStep step)
    {
        Camera mainCam = Camera.main;
        var controller = externalCameraController as CameraController;
        if (controller) controller.enabled = false;

        Vector3 targetPos = step.focusTarget.transform.position;
        float pitchRad = step.cameraAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(0, Mathf.Sin(pitchRad), -Mathf.Cos(pitchRad)) * step.cameraDistance;
        Vector3 finalPos = targetPos + offset;

        float elapsed = 0;
        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;
        Quaternion finalRot = Quaternion.LookRotation(targetPos - finalPos);

        while (elapsed < 1.0f)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed);
            mainCam.transform.position = Vector3.Lerp(startPos, finalPos, t);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, finalRot, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (controller)
        {
            controller.SyncTutorialFocus(targetPos, step.cameraDistance, step.cameraAngle, mainCam.transform.eulerAngles.y);
            controller.enabled = true;
        }
    }

    public void NextStep() { _currentStepIndex++; PrepareStep(_currentStepIndex); }

    public void SkipCurrentStep() { if (_isTutorialActive) NextStep(); }

    private void CheckBuildingProgress(Placeable data)
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;
        TutorialStep current = steps[_currentStepIndex];
        if (!current.requireBuilding || current.targetBuildings == null || current.targetBuildings.Count == 0) return;

        // 获取当前场上所有的建筑实例
        var allNormal = ResourceManager.Instance.GetAllPlacedBuildings();
        var allTutorial = ResourceManager.Instance.GetAllTutorialBuildings();

        bool allRequirementsMet = true;

        foreach (var req in current.targetBuildings)
        {
            int currentCount = 0;

            if (req.isTutorialBuilding)
            {
                // 在教程建筑列表中查找匹配类型的数量
                currentCount = allTutorial.FindAll(b => b.tutorialType == req.tutorialType).Count;
            }
            else
            {
                // 在普通建筑列表中查找匹配类型的数量
                currentCount = allNormal.FindAll(b => b.type == req.normalType).Count;
            }

            if (currentCount < req.requiredCount)
            {
                allRequirementsMet = false;
                break;
            }
        }

        if (allRequirementsMet)
        {
            NextStep();
        }
    }

    private void CheckRemovalProgress()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;
        if (steps[_currentStepIndex].requireRemoval) NextStep();
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false; tutorialUI.Hide();
        if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        if (externalCameraController != null) externalCameraController.enabled = true;
    }
}