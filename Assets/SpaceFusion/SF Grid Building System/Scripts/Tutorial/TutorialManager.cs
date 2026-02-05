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
    private float _stepStartTime;

    // 记录进入步骤时的初始建筑数量快照，用于计算本步增量
    private Dictionary<string, int> _initialCounts = new Dictionary<string, int>();

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
            // 绑定事件：每当建筑状态改变（增加或减少），都去尝试检查一次进度
            PlacementSystem.Instance.OnBuildingPlaced += (data) => OnAnyBuildingAction();
            PlacementSystem.Instance.OnBuildingRemoved += OnAnyBuildingAction;
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
            return;
        }

        // 持续检查数值类非建筑条件（如能量平衡等）
        CheckNonBuildingConditions(currentStep);
    }

    private void OnAnyBuildingAction()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;

        // 延迟到帧末，确保 ResourceManager 已完成列表更新
        StartCoroutine(CheckRequirementsDelayed());
    }

    private IEnumerator CheckRequirementsDelayed()
    {
        yield return new WaitForEndOfFrame();
        if (ResourceManager.Instance == null) yield break;

        TutorialStep current = steps[_currentStepIndex];
        if (IsStepRequirementFulfilled(current))
        {
            NextStep();
        }
    }

    private bool IsStepRequirementFulfilled(TutorialStep step)
    {
        // 只有当该步骤需要建筑任务时才进行增量检查
        if (step.requireBuilding || step.requireTutorialBuilding)
        {
            // 检查普通建筑需求 (通过 ResourceManager 统一查询)
            if (step.houseReq.checkThis && GetCountDelta("House") < step.houseReq.goalCount) return false;
            if (step.farmReq.checkThis && GetCountDelta("Farm") < step.farmReq.goalCount) return false;
            if (step.instituteReq.checkThis && GetCountDelta("Institute") < step.instituteReq.goalCount) return false;
            if (step.powerPlantReq.checkThis && GetCountDelta("PowerPlant") < step.powerPlantReq.goalCount) return false;
            if (step.co2StorageReq.checkThis && GetCountDelta("Co2Storage") < step.co2StorageReq.goalCount) return false;
            if (step.bankReq.checkThis && GetCountDelta("Bank") < step.bankReq.goalCount) return false;

            // 检查教学专用建筑需求
            if (step.localGenReq.checkThis && GetCountDelta("LocalGen") < step.localGenReq.goalCount) return false;
            if (step.batteryReq.checkThis && GetCountDelta("Battery") < step.batteryReq.goalCount) return false;
            if (step.negativeHouseReq.checkThis && GetCountDelta("NegativeHouse") < step.negativeHouseReq.goalCount) return false;
            if (step.ccHouseReq.checkThis && GetCountDelta("CCHouse") < step.ccHouseReq.goalCount) return false;
        }

        return true;
    }

    private int GetCountDelta(string typeKey)
    {
        int currentCount = ResourceManager.Instance.GetTotalBuildingCount(typeKey);
        int initialCount = _initialCounts.ContainsKey(typeKey) ? _initialCounts[typeKey] : 0;
        // 返回当前数量与步骤开始时数量的差值（增量）
        return Mathf.Max(0, currentCount - initialCount);
    }

    private void CheckNonBuildingConditions(TutorialStep currentStep)
    {
        bool statusMet = true;

        if (currentStep.requirePositiveEnergyBalance)
        {
            if (ResourceManager.Instance == null || ResourceManager.Instance.ElectricityBalance < 0f) statusMet = false;
        }

        if (currentStep.requireOptimizationGoal)
        {
            if (LevelScenarioLoader.Instance == null || !LevelScenarioLoader.Instance.IsOptimizationGoalMet()) statusMet = false;
        }

        // 如果步骤没有特定的建筑需求，而是只有数值需求且已达成
        if (statusMet && !currentStep.requireBuilding && !currentStep.requireTutorialBuilding &&
            !currentStep.requireInput && !currentStep.requireRemoval)
        {
            if (Time.time - _stepStartTime > 0.5f) NextStep();
        }
    }

    public void StartTutorial() { _isTutorialActive = true; _currentStepIndex = 0; PrepareStep(0); }

    private void PrepareStep(int index)
    {
        if (index >= steps.Count) { CompleteTutorial(); return; }

        _stepStartTime = Time.time;

        // 进入新步骤前，先记录当前的建筑存量快照
        RecordInitialCounts();

        TutorialStep step = steps[index];

        if (step.clearSceneBeforeStart && TutorialLevelPreparer.Instance != null)
        {
            TutorialLevelPreparer.Instance.ClearAllBuildings();
            if (step.layoutToLoad >= 0)
            {
                TutorialLevelPreparer.Instance.PrepareLayoutForEvent(step.layoutToLoad);
            }
            // 如果清空了场景并加载了预设布局，需要重新获取存量快照
            RecordInitialCounts();
        }

        if (step.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
        {
            _isWaitingForStartCondition = true;
            tutorialUI.Hide();
            if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        }
        else { ActivateStepUI(step); }
    }

    private void RecordInitialCounts()
    {
        _initialCounts.Clear();
        if (ResourceManager.Instance == null) return;

        // 统一查询所有可能涉及的建筑类型名称
        string[] allKeys = { "House", "Farm", "Institute", "PowerPlant", "Co2Storage", "Bank",
                             "LocalGen", "Battery", "NegativeHouse", "CCHouse" };
        foreach (var key in allKeys)
        {
            _initialCounts[key] = ResourceManager.Instance.GetTotalBuildingCount(key);
        }
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

    private void OnAnyBuildingAction(Placeable data = null) => OnAnyBuildingAction();

    private void OnBuildingRemovedTrigger()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _currentStepIndex >= steps.Count) return;

        if (steps[_currentStepIndex].requireRemoval && Time.time - _stepStartTime > 0.5f)
        {
            NextStep();
        }
    }

    private void CompleteTutorial()
    {
        _isTutorialActive = false; tutorialUI.Hide();
        if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        if (externalCameraController != null) externalCameraController.enabled = true;
    }
}