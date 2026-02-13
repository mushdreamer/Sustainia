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
    private bool _isSystemClearing = false;
    private Coroutine _cameraCoroutine;
    private float _stepStartTime;

    private Dictionary<string, int> _initialCounts = new Dictionary<string, int>();
    private int _initialTotalCount = 0;

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
            PlacementSystem.Instance.OnBuildingPlaced += (data) => OnAnyBuildingAction();
            PlacementSystem.Instance.OnBuildingRemoved += () => {
                OnAnyBuildingAction();
                OnBuildingRemovedTrigger();
            };
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
            else if (currentStep.startCondition == TutorialStep.StartCondition.WaitForElectricityOverload)
                met = IsAnyBatteryOverloaded();
            else met = true;

            if (met) ActivateStepUI(currentStep);
            return;
        }

        // --- Update 核心：静默检查 ---
        CheckNonBuildingConditions(currentStep);
    }

    private void OnAnyBuildingAction()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _isSystemClearing || _currentStepIndex >= steps.Count) return;
        StartCoroutine(CheckRequirementsDelayed());
    }

    private IEnumerator CheckRequirementsDelayed()
    {
        yield return new WaitForEndOfFrame();
        if (ResourceManager.Instance == null) yield break;

        TutorialStep current = steps[_currentStepIndex];

        // 只有玩家手动操作时，才打印详细诊断报告
        string report;
        bool allFulfilled = ValidateAllRequirements(current, out report);

        if (allFulfilled)
        {
            Debug.Log($"<color=green>[Tutorial]</color> Step {_currentStepIndex} PASS: 玩家操作后所有条件已达成。");
            NextStep();
        }
        else
        {
            Debug.Log(report);
        }
    }

    // --- 核心修复：统一判定逻辑，消除双重标准 ---
    private bool ValidateAllRequirements(TutorialStep step, out string report)
    {
        report = $"<b>[Step {_currentStepIndex} 诊断报告]</b>\n";
        bool isPass = true;
        float epsilon = 0.01f; // 浮点数精度容错

        // 1. 建筑检查
        if (step.requireBuilding || step.requireTutorialBuilding)
        {
            bool bPass = true;
            var checks = new Dictionary<string, BuildingCheck> {
                { "House", step.houseReq }, { "Farm", step.farmReq }, { "Institute", step.instituteReq },
                { "PowerPlant", step.powerPlantReq }, { "Co2Storage", step.co2StorageReq }, { "Bank", step.bankReq },
                { "LocalGen", step.localGenReq }, { "Battery", step.batteryReq },
                { "NegativeHouse", step.negativeHouseReq }, { "CCHouse", step.ccHouseReq }
            };

            bool hasSpecific = false;
            foreach (var check in checks)
            {
                if (check.Value.checkThis)
                {
                    hasSpecific = true;
                    int delta = GetCountDelta(check.Key);
                    if (delta < check.Value.goalCount) { bPass = false; report += $"  - {check.Key}: FAIL ({delta}/{check.Value.goalCount})\n"; }
                    else report += $"  - {check.Key}: OK\n";
                }
            }
            if (!hasSpecific && GetTotalCurrentCount() <= _initialTotalCount) bPass = false;
            if (!bPass) isPass = false;
        }

        // 2. 数值检查 (统一调用 ResourceManager.Instance)
        if (step.requireFoodSatisfied)
        {
            bool ok = ResourceManager.Instance.FoodBalance >= -epsilon;
            report += $"  - 食物满足: {(ok ? "OK" : "FAIL")} ({ResourceManager.Instance.FoodBalance:F1})\n";
            if (!ok) isPass = false;
        }
        if (step.requireElecStable)
        {
            bool ok = ResourceManager.Instance.ElectricityBalance >= -epsilon;
            report += $"  - 电力稳定: {(ok ? "OK" : "FAIL")} ({ResourceManager.Instance.ElectricityBalance:F1})\n";
            if (!ok) isPass = false;
        }
        if (step.requireCo2WithinLimit)
        {
            if (LevelScenarioLoader.Instance?.currentLevel != null)
            {
                float cur = ResourceManager.Instance.GetCurrentNetEmission();
                float limit = LevelScenarioLoader.Instance.currentLevel.goalCo2;
                bool ok = cur <= limit + epsilon;
                report += $"  - CO2限制: {(ok ? "OK" : "FAIL")} (当前:{cur:F1}, 限制:{limit:F1})\n";
                if (!ok) isPass = false;
            }
            else isPass = false;
        }

        return isPass;
    }

    private void CheckNonBuildingConditions(TutorialStep currentStep)
    {
        if (ResourceManager.Instance == null) return;

        // 如果该步骤需要手动盖建筑或移除，则不自动跳关，必须通过 Action 诊断
        if (currentStep.requireBuilding || currentStep.requireTutorialBuilding || currentStep.requireRemoval || currentStep.requireInput) return;

        // 只针对纯数值任务进行 Update 自动跳转
        if (ValidateAllRequirements(currentStep, out _))
        {
            if (Time.time - _stepStartTime > 0.8f)
            {
                Debug.Log($"<color=green>[Tutorial]</color> Step {_currentStepIndex} 自动达标过关。");
                NextStep();
            }
        }
    }

    private int GetTotalCurrentCount()
    {
        int total = 0;
        string[] allKeys = { "House", "Farm", "Institute", "PowerPlant", "Co2Storage", "Bank", "LocalGen", "Battery", "NegativeHouse", "CCHouse" };
        foreach (var key in allKeys) total += ResourceManager.Instance.GetTotalBuildingCount(key);
        return total;
    }

    private int GetCountDelta(string typeKey)
    {
        int currentCount = ResourceManager.Instance.GetTotalBuildingCount(typeKey);
        int initialCount = _initialCounts.ContainsKey(typeKey) ? _initialCounts[typeKey] : 0;
        return Mathf.Max(0, currentCount - initialCount);
    }

    private bool IsAnyBatteryOverloaded()
    {
        if (ResourceManager.Instance == null) return false;
        float currentBalance = ResourceManager.Instance.ElectricityBalance;
        foreach (var tb in ResourceManager.Instance.GetAllTutorialBuildings())
        {
            if (tb != null && tb.tutorialType == TutorialBuildingType.Battery && currentBalance > tb.GetEffectiveThreshold()) return true;
        }
        return false;
    }

    public void StartTutorial() { _isTutorialActive = true; _currentStepIndex = 0; PrepareStep(0); }

    private void PrepareStep(int index)
    {
        if (index >= steps.Count) { CompleteTutorial(); return; }
        StartCoroutine(PrepareStepRoutine(index));
    }

    private IEnumerator PrepareStepRoutine(int index)
    {
        _stepStartTime = Time.time;
        TutorialStep step = steps[index];
        Debug.Log($"<color=yellow>[Tutorial]</color> Preparing Step {index}: {step.stepName}");

        if (step.clearSceneBeforeStart && TutorialLevelPreparer.Instance != null)
        {
            _isSystemClearing = true;
            TutorialLevelPreparer.Instance.ClearAllBuildings();
            if (step.layoutToLoad >= 0) TutorialLevelPreparer.Instance.PrepareLayoutForEvent(step.layoutToLoad);
            yield return new WaitForEndOfFrame();
            yield return new WaitForFixedUpdate();
            RecordInitialCounts();
            _isSystemClearing = false;
        }
        else RecordInitialCounts();

        if (step.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit ||
            step.startCondition == TutorialStep.StartCondition.WaitForElectricityOverload)
        {
            _isWaitingForStartCondition = true;
            tutorialUI.Hide();
            if (ResourceManager.Instance != null) ResourceManager.Instance.isPaused = false;
        }
        else ActivateStepUI(step);
    }

    private void RecordInitialCounts()
    {
        _initialCounts.Clear(); _initialTotalCount = 0;
        if (ResourceManager.Instance == null) return;
        string[] allKeys = { "House", "Farm", "Institute", "PowerPlant", "Co2Storage", "Bank", "LocalGen", "Battery", "NegativeHouse", "CCHouse" };
        foreach (var key in allKeys)
        {
            int count = ResourceManager.Instance.GetTotalBuildingCount(key);
            _initialCounts[key] = count; _initialTotalCount += count;
        }
    }

    private void ActivateStepUI(TutorialStep step)
    {
        _isWaitingForStartCondition = false;
        if (step.setSpecificMoney && ResourceManager.Instance != null)
            ResourceManager.Instance.SetMoneyDirectly(step.moneyAmount);

        tutorialUI.ShowStep(step);
        if (step.focusTarget != null) UpdateZoneHighlights(step.focusTarget, step.indicatorColor, step.showIndicator);
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
            elapsed += Time.unscaledDeltaTime; yield return null;
        }
        if (controller)
        {
            controller.SyncTutorialFocus(targetPos, step.cameraDistance, step.cameraAngle, mainCam.transform.eulerAngles.y);
            controller.enabled = true;
        }
    }

    public void NextStep() { _currentStepIndex++; PrepareStep(_currentStepIndex); }

    public void SkipCurrentStep() { if (_isTutorialActive) NextStep(); }

    private void OnBuildingRemovedTrigger()
    {
        if (!_isTutorialActive || _isWaitingForStartCondition || _isSystemClearing || _currentStepIndex >= steps.Count) return;
        if (steps[_currentStepIndex].requireRemoval && Time.time - _stepStartTime > 0.5f)
        {
            Debug.Log($"<color=green>[Tutorial]</color> Step {_currentStepIndex} completed via Building Removal.");
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