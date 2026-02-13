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

        [Header("UI References")]
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI populationText;
        public TextMeshProUGUI foodText;
        public TextMeshProUGUI electricityText;
        public TextMeshProUGUI dayText;
        public TextMeshProUGUI co2EmissionText;

        [Header("State Values")]
        public int _currentDay;
        private float _money;
        public int _currentPopulation;
        private int _populationCapacity;
        private int _basePopulation;

        private float _currentFoodProduction = 0f;
        private float _currentFoodDemand = 0f;
        public float FoodBalance => _currentFoodProduction - _currentFoodDemand;

        private float _currentLocalGeneration = 0f;
        private float _currentTotalDemand = 0f;
        public float ElectricityBalance => _currentLocalGeneration - _currentTotalDemand;

        [Header("Custom Balance Thresholds")]
        public float globalOverloadThreshold = 0f;

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

        [Header("Event Teaching Logic (S-Formula)")]
        public float w1 = 1.0f;
        public float w2 = 1.0f;

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

        private float _populationGrowthProgress = 0f;
        private float _populationDecreaseProgress = 0f;

        public float Money => _money;

        // --- S 公式相关的实时属性 ---
        public float CurrentGoldOutput => _money;
        public float CurrentGreenScore => _carbonDioxideAbsorption * 5.0f;
        public float ProsperityScoreS => (w1 * CurrentGoldOutput) + (w2 * CurrentGreenScore);

        // --- P 公式的实时属性 ---
        public float CurrentPValue
        {
            get
            {
                float val = GetTotalBuildingCount("House") * 10f;
                float fgap = Mathf.Abs(Mathf.Min(0, FoodBalance));
                float egap = Mathf.Abs(Mathf.Min(0, ElectricityBalance));
                float totalGap = (fgap + egap) * 2.0f;
                return val - totalGap;
            }
        }

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
            _buildingCounts.Clear();
            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType))) _buildingCounts[type] = 0;

            UpdateUI();
            InvokeRepeating(nameof(Tick), 1f, 1f);
        }

        private void Tick()
        {
            if (isPaused) return;
            _currentDay++;
            _carbonDioxideEmission = _baseCarbonDioxideEmission * _co2EmissionModifier;

            if (FoodBalance >= 0)
            {
                if (_bankCount > 0 && _currentFoodDemand > 0) AddMoney(_currentFoodDemand * moneyMultiplierFromFood);
                if (_currentPopulation < _populationCapacity)
                {
                    _populationGrowthProgress += populationGrowthRate;
                    if (_populationGrowthProgress >= 1f) { _currentPopulation++; _populationGrowthProgress -= 1f; }
                }
            }
            else if (_currentPopulation > _basePopulation)
            {
                _populationDecreaseProgress += populationDecreaseRate;
                if (_populationDecreaseProgress >= 1f) { _currentPopulation--; _populationDecreaseProgress -= 1f; }
            }

            float netEmission = GetCurrentNetEmission();
            if (_airQuality > 70) _happiness += happinessChangeRate;
            else if (_airQuality < 40) _happiness -= happinessChangeRate * 1.5f;
            _happiness = Mathf.Clamp(_happiness, 0f, 100f);

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

            if (foodText != null)
            {
                bool isSatisfied = FoodBalance >= -0.01f;
                string foodColor = isSatisfied ? "<color=green>" : "<color=red>";
                foodText.text = $"Food Balance: {foodColor}{(FoodBalance >= 0 ? "+" : "")}{FoodBalance:F1}</color>";
            }

            if (electricityText != null)
            {
                bool isStable = (ElectricityBalance >= -0.01f) && !IsOverloaded();
                string statusText = IsOverloaded() ? "Overload" : (isStable ? "Stable" : "Shortage");
                string elecColor = isStable ? "<color=green>" : "<color=red>";
                electricityText.text = $"Elec Balance: {elecColor}{statusText} ({(ElectricityBalance >= 0 ? "+" : "")}{ElectricityBalance:F1})</color>";
            }

            if (co2EmissionText != null)
            {
                float currentNetCo2 = GetCurrentNetEmission();
                string co2String = $"CO2: {currentNetCo2:F1}";
                if (currentLevel != null)
                {
                    bool isUnder = currentNetCo2 <= currentLevel.goalCo2 + 0.01f;
                    co2String = $"{(isUnder ? "<color=green>" : "<color=red>")}{co2String}</color> / Limit: {currentLevel.goalCo2:F0}";
                }
                co2EmissionText.text = co2String;
            }

            if (dayText != null) dayText.text = $"Day {_currentDay}";
        }

        // --- 核心方法 ---
        public void SetMoneyDirectly(float amount) { _money = amount; UpdateUI(); }
        public void AddMoney(float amount) { _money += amount; UpdateUI(); }
        public bool SpendMoney(float amount) { if (_money >= amount) { _money -= amount; UpdateUI(); return true; } return false; }
        public float GetCurrentNetEmission() => (_baseCarbonDioxideEmission * _co2EmissionModifier) - _carbonDioxideAbsorption;

        public void AddHouseEffect(int cap, int initPop) { _populationCapacity += cap; _currentPopulation += initPop; _basePopulation += initPop; UpdateUI(); }
        public void RemoveHouseEffect(int cap, int initPop) { _populationCapacity -= cap; _basePopulation -= initPop; _currentPopulation = Mathf.Min(_currentPopulation, _populationCapacity); UpdateUI(); }

        public void AddFoodProduction(float a) { _currentFoodProduction += a; UpdateUI(); }
        public void RemoveFoodProduction(float a) { _currentFoodProduction -= a; UpdateUI(); }
        public void AddFoodDemand(float a) { _currentFoodDemand += a; UpdateUI(); }
        public void RemoveFoodDemand(float a) { _currentFoodDemand -= a; UpdateUI(); }

        public void AddPowerPlantEffect(float co2) { _baseCarbonDioxideEmission += co2; UpdateUI(); }
        public void RemovePowerPlantEffect(float co2) { _baseCarbonDioxideEmission -= co2; UpdateUI(); }
        public void AddCo2Absorption(float a) { _carbonDioxideAbsorption += a; UpdateUI(); }
        public void RemoveCo2Absorption(float a) { _carbonDioxideAbsorption -= a; UpdateUI(); }

        public void AddGeneration(float a) { _currentLocalGeneration += a; UpdateUI(); }
        public void RemoveGeneration(float a) { _currentLocalGeneration -= a; UpdateUI(); }
        public void AddConsumption(float a) { _currentTotalDemand += a; UpdateUI(); }
        public void RemoveConsumption(float a) { _currentTotalDemand -= a; UpdateUI(); }

        public void AddBank() => _bankCount++;
        public void RemoveBank() => _bankCount--;

        public bool IsOverloaded()
        {
            float balance = ElectricityBalance;
            _allTutorialBuildings.RemoveAll(item => item == null);
            foreach (var tb in _allTutorialBuildings)
            {
                if (tb != null && tb.tutorialType == TutorialBuildingType.Battery)
                {
                    float t = tb.GetEffectiveThreshold();
                    if (t > 0.01f && balance > t + 0.01f) return true;
                }
            }
            return globalOverloadThreshold > 0.01f && balance > globalOverloadThreshold + 0.01f;
        }

        public float GetActiveOverloadThreshold()
        {
            _allTutorialBuildings.RemoveAll(item => item == null);
            foreach (var tb in _allTutorialBuildings)
            {
                if (tb != null && tb.tutorialType == TutorialBuildingType.Battery)
                {
                    float t = tb.GetEffectiveThreshold();
                    if (t > 0.01f) return t;
                }
            }
            return globalOverloadThreshold;
        }

        public void RegisterBuilding(BuildingType type) { if (_buildingCounts.ContainsKey(type)) _buildingCounts[type]++; }
        public void UnregisterBuilding(BuildingType type) { if (_buildingCounts.ContainsKey(type)) { _buildingCounts[type]--; if (_buildingCounts[type] < 0) _buildingCounts[type] = 0; } }

        public void RegisterBuildingInstance(BuildingEffect b) { if (!_allPlacedBuildings.Contains(b)) _allPlacedBuildings.Add(b); }
        public void UnregisterBuildingInstance(BuildingEffect b) { _allPlacedBuildings.Remove(b); }
        public void RegisterTutorialBuildingInstance(TutorialBuildingEffect b) { if (!_allTutorialBuildings.Contains(b)) _allTutorialBuildings.Add(b); }
        public void UnregisterTutorialBuildingInstance(TutorialBuildingEffect b) { _allTutorialBuildings.Remove(b); }

        public List<BuildingEffect> GetAllPlacedBuildings() { _allPlacedBuildings.RemoveAll(i => i == null); return new List<BuildingEffect>(_allPlacedBuildings); }
        public List<TutorialBuildingEffect> GetAllTutorialBuildings() { _allTutorialBuildings.RemoveAll(i => i == null); return new List<TutorialBuildingEffect>(_allTutorialBuildings); }

        public int GetTotalBuildingCount(string typeKey)
        {
            int count = 0;
            _allPlacedBuildings.RemoveAll(i => i == null);
            foreach (var b in _allPlacedBuildings) if (b.type.ToString() == typeKey) count++;
            _allTutorialBuildings.RemoveAll(i => i == null);
            foreach (var tb in _allTutorialBuildings) if (tb.tutorialType.ToString() == typeKey) count++;
            return count;
        }
    }
}