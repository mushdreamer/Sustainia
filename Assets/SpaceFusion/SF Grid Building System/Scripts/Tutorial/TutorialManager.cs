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
    private float _stepStartTime; // 用于防止自跳步的时间闸门

    private void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    private void Start()
    {
        if (!enableTutorial) { if (tutorialUI) tutorialUI.Hide(); return; }
        if (tutorialUI) tutorialUI.Initialize(this);
        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.OnBuildingPlaced += OnBuildingPlacedTrigger;
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

        _stepStartTime = Time.time; // 重置时间闸门
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

    private void OnBuildingPlacedTrigger(Placeable data)
    {
        // 收到放置事件后，开启一个协程等待一帧后再检查，确保建筑已注册
        StartCoroutine(CheckProgressWithDelay());
    }

    private IEnumerator CheckProgressWithDelay()
    {
        // 等待一帧，确保新建筑的 Start() 运行并完成了 ResourceManager 的注册
        yield return null;

        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) yield break;

        // 防御：防止由于强制生成（ForceSpawn）导致的瞬间跳步
        if (Time.time - _stepStartTime < 0.2f) yield break;

        TutorialStep current = steps[_currentStepIndex];
        if (!current.requireBuilding || current.targetBuildings == null || current.targetBuildings.Count == 0) yield break;

        var allNormal = ResourceManager.Instance.GetAllPlacedBuildings();
        var allTutorial = ResourceManager.Instance.GetAllTutorialBuildings();

        if (current.allowAnyCombination)
        {
            // 模式 A：灵活总数模式。计算列表中所有建筑在场上的总和是否达到要求的总和。
            int totalRequired = 0;
            int totalCurrentFound = 0;

            foreach (var req in current.targetBuildings)
            {
                totalRequired += req.requiredCount;
                if (req.isTutorialBuilding)
                    totalCurrentFound += allTutorial.FindAll(b => b.tutorialType == req.tutorialType).Count;
                else
                    totalCurrentFound += allNormal.FindAll(b => b.type == req.normalType).Count;
            }

            if (totalCurrentFound >= totalRequired) NextStep();
        }
        else
        {
            // 模式 B：严格模式。列表中每一项配置都必须分别满足数量要求。
            bool allMet = true;
            foreach (var req in current.targetBuildings)
            {
                int count = req.isTutorialBuilding ?
                    allTutorial.FindAll(b => b.tutorialType == req.tutorialType).Count :
                    allNormal.FindAll(b => b.type == req.normalType).Count;

                if (count < req.requiredCount)
                {
                    allMet = false;
                    break;
                }
            }

            if (allMet) NextStep();
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