using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;
using SpaceFusion.SF_Grid_Building_System.Scripts.Scriptables;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Managers
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance;

        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI populationText;
        public TextMeshProUGUI foodText;
        public TextMeshProUGUI electricityText;
        public TextMeshProUGUI dayText;
        public TextMeshProUGUI co2EmissionText;

        public int _currentDay;
        private float _money;
        public int _currentPopulation;
        private int _populationCapacity;
        private int _basePopulation;

        // --- 食物逻辑修改：完全模仿电力系统 ---
        private float _currentFoodProduction = 0f; // 对应产电
        private float _currentFoodDemand = 0f;     // 对应耗电
        public float FoodBalance => _currentFoodProduction - _currentFoodDemand;

        private float _currentLocalGeneration = 0f;
        private float _currentTotalDemand = 0f;
        public float ElectricityBalance => _currentLocalGeneration - _currentTotalDemand;

        public float _happiness;
        private float _baseCarbonDioxideEmission;
        private float _carbonDioxideEmission;
        public float _airQuality;
        private float _carbonDioxideAbsorption = 0f;
        private int _bankCount = 0;
        private int _universityLevel = 1;

        private List<BuildingEffect> _allPlacedBuildings = new List<BuildingEffect>();
        private List<TutorialBuildingEffect> _allTutorialBuildings = new List<TutorialBuildingEffect>();
        private Dictionary<BuildingType, int> _buildingCounts = new Dictionary<BuildingType, int>();

        private float _co2EmissionModifier = 1.0f;
        public bool isPaused = false;

        [Header("Game Balance Settings")]
        public float startingMoney = 1000f;
        public int startingPopulationCapacity = 10;
        public float populationDecreaseRate = 0.2f;
        public float foodConsumptionPerPerson = 0.1f;
        public float populationGrowthRate = 0.5f;
        public float moneyMultiplierFromFood = 0.5f;

        [Header("Electricity & Environment")]
        public float happinessChangeRate = 1f;
        public float airQualityRecoveryRate = 0.1f;
        public float airQualityDeclineRate = 0.2f;

        [Header("Research Settings")]
        public float researchCostBase = 100f;
        public float researchCostMultiplier = 1.5f;
        public int researchLevelCap = 10;
        public float powerEfficiencyGain = 0.1f;
        public float co2EmissionReduction = 0.1f;

        private float _populationGrowthProgress = 0f;
        private float _populationDecreaseProgress = 0f;

        public float Money => _money;
        public int UniversityLevel => _universityLevel;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            _currentDay = 1;
            _money = startingMoney;
            _populationCapacity = startingPopulationCapacity;
            _happiness = 100f;
            _airQuality = 100f;
            _universityLevel = 1;

            _currentLocalGeneration = 0f;
            _currentTotalDemand = 0f;
            _currentFoodProduction = 0f;
            _currentFoodDemand = 0f;

            _buildingCounts.Clear();
            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
            {
                _buildingCounts[type] = 0;
            }

            UpdateUI();
            InvokeRepeating(nameof(Tick), 1f, 1f);
        }

        private void Tick()
        {
            if (isPaused) return;
            _currentDay++;
            _carbonDioxideEmission = _baseCarbonDioxideEmission * _co2EmissionModifier;

            // --- 食物平衡影响人口逻辑 ---
            if (FoodBalance >= 0)
            {
                // 食物充足（产大于需）：产生经济效益（如果有银行）并增长人口
                if (_bankCount > 0 && _currentFoodDemand > 0)
                    AddMoney(_currentFoodDemand * moneyMultiplierFromFood);

                if (_currentPopulation < _populationCapacity && (_currentPopulation > 0 || _currentFoodProduction > 0))
                {
                    _populationGrowthProgress += populationGrowthRate;
                    if (_populationGrowthProgress >= 1f) { _currentPopulation++; _populationGrowthProgress -= 1f; }
                }
            }
            else
            {
                // 食物短缺（需大于产）：人口由于饥饿减少
                if (_currentPopulation > _basePopulation)
                {
                    _populationDecreaseProgress += populationDecreaseRate;
                    if (_populationDecreaseProgress >= 1f) { _currentPopulation--; _populationDecreaseProgress -= 1f; }
                }
            }

            if (_airQuality > 70) _happiness += happinessChangeRate;
            else if (_airQuality < 40) _happiness -= happinessChangeRate * 1.5f;
            _happiness = Mathf.Clamp(_happiness, 0f, 100f);

            float netEmission = _carbonDioxideEmission - _carbonDioxideAbsorption;
            if (netEmission > 0) _airQuality -= netEmission * airQualityDeclineRate;
            _airQuality += airQualityRecoveryRate;
            _airQuality = Mathf.Clamp(_airQuality, 0f, 100f);

            UpdateUI();
        }

        private void UpdateUI()
        {
            OptimizationLevelData currentLevel = null;
            if (LevelScenarioLoader.Instance != null) currentLevel = LevelScenarioLoader.Instance.currentLevel;

            if (moneyText != null) moneyText.text = $"Money: {_money:F0}";
            if (populationText != null) populationText.text = $"Population: {_currentPopulation} / {_populationCapacity}";

            // --- 食物 UI 逻辑：现在和电力完全一样 ---
            if (foodText != null)
            {
                string sign = FoodBalance >= 0 ? "+" : "";
                string foodColor = FoodBalance >= 0 ? "<color=green>" : "<color=red>";
                foodText.text = $"Food Balance: {foodColor}{sign}{FoodBalance:F1}</color>\n<size=70%>(Prod: {_currentFoodProduction:F1} | Dem: {_currentFoodDemand:F1})</size>";
            }

            if (electricityText != null)
            {
                string sign = ElectricityBalance >= 0 ? "+" : "";
                string elecColor = ElectricityBalance >= 0 ? "<color=green>" : "<color=red>";
                string elecString = $"Elec Balance: {elecColor}{sign}{ElectricityBalance:F1}</color>";

                if (currentLevel != null)
                {
                    bool isEnergyMet = IsGoalMet(ElectricityBalance, currentLevel.goalEnergy, currentLevel.successTolerancePercent, true);
                    string goalColor = isEnergyMet ? "<color=#00FF00>" : "<color=#FF8888>";
                    elecString += $" / {goalColor}Goal: {currentLevel.goalEnergy:F0}</color>";
                }
                elecString += $"\n<size=70%>(Gen: {_currentLocalGeneration:F1} | Dem: {_currentTotalDemand:F1})</size>";
                electricityText.text = elecString;
            }

            if (co2EmissionText != null)
            {
                float currentNetCo2 = (_baseCarbonDioxideEmission * _co2EmissionModifier) - _carbonDioxideAbsorption;
                string co2String = $"CO2 Emission: {currentNetCo2:F1}";

                if (currentLevel != null)
                {
                    bool isCo2Met = IsGoalMet(currentNetCo2, currentLevel.goalCo2, currentLevel.successTolerancePercent, false);
                    string goalColor = isCo2Met ? "<color=#00FF00>" : "<color=#FF8888>";
                    co2String += $" / {goalColor}Goal: <{currentLevel.goalCo2:F0}</color>";
                }
                co2EmissionText.text = co2String;
            }
            if (dayText != null) dayText.text = $"Day {_currentDay}";
        }

        private bool IsGoalMet(float current, float target, float tolerancePercent, bool requireHigher)
        {
            float diff = Mathf.Abs(current - target);
            float allowedDiff = Mathf.Abs(target * tolerancePercent);
            if (target == 0) allowedDiff = 2f;
            if (diff <= allowedDiff) return true;
            return requireHigher ? current > target : current < target;
        }

        public bool SpendMoney(float amount)
        {
            if (_money >= amount) { _money -= amount; UpdateUI(); return true; }
            return false;
        }

        public void AddMoney(float amount) { _money += amount; UpdateUI(); }

        public void AddHouseEffect(int capacityIncrease, int initialPopulation)
        {
            _populationCapacity += capacityIncrease;
            _currentPopulation += initialPopulation;
            _basePopulation += initialPopulation;
            _currentPopulation = Mathf.Min(_currentPopulation, _populationCapacity);
            UpdateUI();
        }

        public void RemoveHouseEffect(int capacityDecrease, int initialPopulationDecrease)
        {
            _populationCapacity -= capacityDecrease;
            _basePopulation -= initialPopulationDecrease;
            if (_basePopulation < 0) _basePopulation = 0;
            _currentPopulation = Mathf.Min(_currentPopulation, _populationCapacity);
            UpdateUI();
        }

        // --- 食物接口：完全模仿电力的 AddGeneration/AddConsumption ---
        public void AddFoodProduction(float amount) { _currentFoodProduction += amount; UpdateUI(); }
        public void RemoveFoodProduction(float amount) { _currentFoodProduction -= amount; if (_currentFoodProduction < 0) _currentFoodProduction = 0; UpdateUI(); }
        public void AddFoodDemand(float amount) { _currentFoodDemand += amount; UpdateUI(); }
        public void RemoveFoodDemand(float amount) { _currentFoodDemand -= amount; if (_currentFoodDemand < 0) _currentFoodDemand = 0; UpdateUI(); }

        public void AddPowerPlantEffect(float co2) { _baseCarbonDioxideEmission += co2; UpdateUI(); }
        public void RemovePowerPlantEffect(float co2) { _baseCarbonDioxideEmission -= co2; UpdateUI(); }

        public void AddCo2Absorption(float amount) { _carbonDioxideAbsorption += amount; UpdateUI(); }
        public void RemoveCo2Absorption(float amount) { _carbonDioxideAbsorption -= amount; if (_carbonDioxideAbsorption < 0) _carbonDioxideAbsorption = 0; UpdateUI(); }

        public void AddBank() => _bankCount++;
        public void RemoveBank() { _bankCount--; if (_bankCount < 0) _bankCount = 0; }

        public float GetCurrentNetEmission()
        {
            float netEmission = _carbonDioxideEmission - _carbonDioxideAbsorption;
            return (netEmission > 0) ? netEmission : 0;
        }

        public void RegisterBuilding(BuildingType type) { if (_buildingCounts.ContainsKey(type)) _buildingCounts[type]++; }
        public void UnregisterBuilding(BuildingType type) { if (_buildingCounts.ContainsKey(type)) { _buildingCounts[type]--; if (_buildingCounts[type] < 0) _buildingCounts[type] = 0; } }

        public void AddGeneration(float amount) { _currentLocalGeneration += amount; UpdateUI(); }
        public void RemoveGeneration(float amount) { _currentLocalGeneration -= amount; if (_currentLocalGeneration < 0) _currentLocalGeneration = 0; UpdateUI(); }
        public void AddConsumption(float amount) { _currentTotalDemand += amount; UpdateUI(); }
        public void RemoveConsumption(float amount) { _currentTotalDemand -= amount; if (_currentTotalDemand < 0) _currentTotalDemand = 0; UpdateUI(); }

        public void RegisterBuildingInstance(BuildingEffect building) { if (!_allPlacedBuildings.Contains(building)) _allPlacedBuildings.Add(building); }
        public void UnregisterBuildingInstance(BuildingEffect building) { if (_allPlacedBuildings.Contains(building)) _allPlacedBuildings.Remove(building); }

        public void RegisterTutorialBuildingInstance(TutorialBuildingEffect building) { if (!_allTutorialBuildings.Contains(building)) _allTutorialBuildings.Add(building); }
        public void UnregisterTutorialBuildingInstance(TutorialBuildingEffect building) { if (_allTutorialBuildings.Contains(building)) _allTutorialBuildings.Remove(building); }

        public List<BuildingEffect> GetAllPlacedBuildings() { _allPlacedBuildings.RemoveAll(item => item == null); return new List<BuildingEffect>(_allPlacedBuildings); }
        public List<TutorialBuildingEffect> GetAllTutorialBuildings() { _allTutorialBuildings.RemoveAll(item => item == null); return new List<TutorialBuildingEffect>(_allTutorialBuildings); }

        public int GetTotalBuildingCount(string typeKey)
        {
            int count = 0;
            _allPlacedBuildings.RemoveAll(item => item == null);
            foreach (var b in _allPlacedBuildings)
            {
                if (b.type.ToString() == typeKey) count++;
            }
            _allTutorialBuildings.RemoveAll(item => item == null);
            foreach (var tb in _allTutorialBuildings)
            {
                if (tb.tutorialType.ToString() == typeKey) count++;
            }
            return count;
        }
    }
}