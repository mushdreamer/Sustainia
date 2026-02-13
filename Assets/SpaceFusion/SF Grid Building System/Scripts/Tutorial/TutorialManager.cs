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
        if (_isTutorialActive && _currentStepIndex < steps.Count && steps[_currentStepIndex].showFormulaPanel)
        {
            UpdateDynamicFormula(steps[_currentStepIndex]);
        }
        if (Input.GetKeyDown(KeyCode.N)) SkipCurrentStep();
        if (!_isTutorialActive || _currentStepIndex >= steps.Count) return;

        TutorialStep currentStep = steps[_currentStepIndex];

        if (_isWaitingForStartCondition)
        {
            bool met = false;
            if (currentStep.startCondition == TutorialStep.StartCondition.WaitForElectricityDeficit)
                met = ResourceManager.Instance != null && ResourceManager.Instance.ElectricityBalance < -0.1f;
            else if (currentStep.startCondition == TutorialStep.StartCondition.WaitForElectricityOverload)
                met = ResourceManager.Instance != null && ResourceManager.Instance.IsOverloaded();
            else met = true;

            if (met) ActivateStepUI(currentStep);
            return;
        }

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

    private bool ValidateAllRequirements(TutorialStep step, out string report)
    {
        report = $"<b>[Step {_currentStepIndex} 诊断报告]</b>\n";
        bool isPass = true;
        float epsilon = 0.01f;

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

            foreach (var check in checks)
            {
                if (check.Value.checkThis)
                {
                    int delta = GetCountDelta(check.Key);
                    if (delta < check.Value.goalCount) { bPass = false; report += $"  - {check.Key}: FAIL ({delta}/{check.Value.goalCount})\n"; }
                    else report += $"  - {check.Key}: OK\n";
                }
            }
            if (!bPass) isPass = false;
        }

        // 2. 数值检查
        if (step.requireFoodSatisfied)
        {
            bool ok = ResourceManager.Instance.FoodBalance >= -epsilon;
            report += $"  - 食物满足: {(ok ? "OK" : "FAIL")} (Bal: {ResourceManager.Instance.FoodBalance:F1})\n";
            if (!ok) isPass = false;
        }
        if (step.requireFoodShortage)
        {
            bool ok = ResourceManager.Instance.FoodBalance < -epsilon;
            report += $"  - 食物短缺: {(ok ? "OK" : "FAIL")} (Bal: {ResourceManager.Instance.FoodBalance:F1})\n";
            if (!ok) isPass = false;
        }

        float elecBal = ResourceManager.Instance.ElectricityBalance;
        bool isOverloaded = ResourceManager.Instance.IsOverloaded();
        float threshold = ResourceManager.Instance.GetActiveOverloadThreshold();

        report += $"  - 电力诊断: Balance={elecBal:F1}, Threshold={threshold:F1}\n";
        report += $"  - 电力状态: {(isOverloaded ? "<color=red>OVERLOAD</color>" : "<color=green>NORMAL</color>")}\n";

        if (step.requireElecStable || step.requirePositiveEnergyBalance)
        {
            bool ok = elecBal >= -epsilon;
            report += $"    - 判定稳定: {(ok ? "OK" : "FAIL")}\n";
            if (!ok) isPass = false;
        }
        if (step.requireElecDeficit)
        {
            bool ok = elecBal < -epsilon;
            report += $"    - 判定赤字: {(ok ? "OK" : "FAIL")}\n";
            if (!ok) isPass = false;
        }
        if (step.requireElecOverload)
        {
            bool ok = isOverloaded;
            report += $"    - 判定过载: {(ok ? "OK" : "FAIL")}\n";
            if (!ok) isPass = false;
        }
        if (step.requireElecNormal)
        {
            bool ok = !isOverloaded;
            report += $"    - 判定正常: {(ok ? "OK" : "FAIL")}\n";
            if (!ok) isPass = false;
        }

        if (step.requireCo2WithinLimit)
        {
            if (LevelScenarioLoader.Instance?.currentLevel != null)
            {
                float cur = ResourceManager.Instance.GetCurrentNetEmission();
                float limit = LevelScenarioLoader.Instance.currentLevel.goalCo2;
                bool ok = cur <= limit + epsilon;
                report += $"  - CO2限制: {(ok ? "OK" : "FAIL")} (Net:{cur:F1}, Limit:{limit:F1})\n";
                if (!ok) isPass = false;
            }
            else isPass = false;
        }
        if (step.requireCo2OverLimit)
        {
            if (LevelScenarioLoader.Instance?.currentLevel != null)
            {
                float cur = ResourceManager.Instance.GetCurrentNetEmission();
                float limit = LevelScenarioLoader.Instance.currentLevel.goalCo2;
                bool ok = cur > limit + epsilon;
                report += $"  - CO2超标: {(ok ? "OK" : "FAIL")} (Net:{cur:F1}, Limit:{limit:F1})\n";
                if (!ok) isPass = false;
            }
            else isPass = false;
        }

        // --- P值判定逻辑集成 ---
        if (step.requirePValueGoal)
        {
            float currentP = ResourceManager.Instance.CurrentPValue;
            bool ok = currentP >= step.targetPValue - epsilon;
            report += $"  - P值目标: {(ok ? "OK" : "FAIL")} (Current:{currentP:F1}, Target:{step.targetPValue:F1})\n";
            if (!ok) isPass = false;
        }

        if (step.requireOptimizationGoal)
        {
            bool ok = LevelScenarioLoader.Instance != null && LevelScenarioLoader.Instance.IsOptimizationGoalMet();
            report += $"  - 优化目标: {(ok ? "OK" : "FAIL")}\n";
            if (!ok) isPass = false;
        }

        return isPass;
    }

    private void CheckNonBuildingConditions(TutorialStep currentStep)
    {
        if (ResourceManager.Instance == null) return;
        if (currentStep.requireBuilding || currentStep.requireTutorialBuilding || currentStep.requireRemoval || currentStep.requireInput) return;

        if (ValidateAllRequirements(currentStep, out _))
        {
            if (Time.time - _stepStartTime > 0.8f)
            {
                Debug.Log($"<color=green>[Tutorial]</color> Step {_currentStepIndex} 自动达标过关。");
                NextStep();
            }
        }
    }

    private int GetCountDelta(string typeKey)
    {
        int currentCount = ResourceManager.Instance.GetTotalBuildingCount(typeKey);
        int initialCount = _initialCounts.ContainsKey(typeKey) ? _initialCounts[typeKey] : 0;
        return Mathf.Max(0, currentCount - initialCount);
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
        if (step.setSpecificMoney && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddMoney(step.moneyAmount - ResourceManager.Instance.Money);
        }
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
        _initialCounts.Clear();
        if (ResourceManager.Instance == null) return;
        string[] allKeys = { "House", "Farm", "Institute", "PowerPlant", "Co2Storage", "Bank", "LocalGen", "Battery", "NegativeHouse", "CCHouse" };
        int total = 0;
        foreach (var key in allKeys)
        {
            int count = ResourceManager.Instance.GetTotalBuildingCount(key);
            _initialCounts[key] = count; total += count;
        }
        _initialTotalCount = total;
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

    private void UpdateDynamicFormula(TutorialStep step)
    {
        if (ResourceManager.Instance == null || string.IsNullOrEmpty(step.formulaContent)) return;

        string content = step.formulaContent;

        // --- [严格保留：原有 P 值实时逻辑] ---
        float val = ResourceManager.Instance.GetTotalBuildingCount("House") * 10f;
        float fgap = Mathf.Abs(Mathf.Min(0, ResourceManager.Instance.FoodBalance));
        float egap = Mathf.Abs(Mathf.Min(0, ResourceManager.Instance.ElectricityBalance));
        float totalGap = (fgap + egap) * 2.0f;
        float p = ResourceManager.Instance.CurrentPValue;

        content = content.Replace("{val}", val.ToString("F0"));
        content = content.Replace("{fgap}", fgap.ToString("F1"));
        content = content.Replace("{egap}", egap.ToString("F1"));
        content = content.Replace("{gap}", totalGap.ToString("F1"));
        content = content.Replace("{p}", p.ToString("F1"));
        content = content.Replace("{-p}", (-p).ToString("F1"));

        // --- [新增：S 值实时逻辑] ---
        // 1. 实时计算权重 w1 和 w2 (基于建筑数量)
        int factories = ResourceManager.Instance.GetTotalBuildingCount("PowerPlant");
        int ccus = ResourceManager.Instance.GetTotalBuildingCount("Co2Storage");

        ResourceManager.Instance.w1 = 1.0f + (factories * 0.5f);
        ResourceManager.Instance.w2 = 1.0f + (ccus * 0.5f);

        // 2. 获取实时数值
        float s = ResourceManager.Instance.ProsperityScoreS;
        float w1 = ResourceManager.Instance.w1;
        float w2 = ResourceManager.Instance.w2;
        float gold = ResourceManager.Instance.CurrentGoldOutput;
        float green = ResourceManager.Instance.CurrentGreenScore;

        // 3. 执行替换
        content = content.Replace("{w1}", w1.ToString("F1"));
        content = content.Replace("{w2}", w2.ToString("F1"));
        content = content.Replace("{gold}", gold.ToString("F1"));
        content = content.Replace("{green}", green.ToString("F1"));
        content = content.Replace("{s}", s.ToString("F1"));

        // --- [资源状态实时替换] ---
        float food = ResourceManager.Instance.FoodBalance;
        float elec = ResourceManager.Instance.ElectricityBalance;
        content = content.Replace("{food}", (food >= 0 ? "+" : "") + food.ToString("F1"));
        content = content.Replace("{elec}", (elec >= 0 ? "+" : "") + elec.ToString("F1"));

        // --- [推送到 UI] ---
        if (FormulaUI.Instance != null)
        {
            FormulaUI.Instance.UpdateFormulaText(content);
        }
    }

    private void UpdateTeachingWeights()
    {
        if (ResourceManager.Instance == null) return;

        // 逻辑：每多一个发电厂，w1增加0.5；每多一个碳捕集，w2增加0.5
        int factories = ResourceManager.Instance.GetTotalBuildingCount("PowerPlant");
        int ccus = ResourceManager.Instance.GetTotalBuildingCount("Co2Storage");

        ResourceManager.Instance.w1 = 1.0f + (factories * 0.5f);
        ResourceManager.Instance.w2 = 1.0f + (ccus * 0.5f);
    }
}