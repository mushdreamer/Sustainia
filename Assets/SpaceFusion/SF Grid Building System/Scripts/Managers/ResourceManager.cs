using UnityEngine;
using TMPro; // 用于UI显示
using UnityEngine.UI;
using System.Collections.Generic;
using SpaceFusion.SF_Grid_Building_System.Scripts.Core;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Managers
{
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance;

        // --- UI Fields (在编辑器中拖拽对应的UI Text组件) ---
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI populationText;
        public TextMeshProUGUI foodText;
        public TextMeshProUGUI electricityText;
        public TextMeshProUGUI happinessText;
        public Slider happinessSlider;
        public TextMeshProUGUI airQualityText;
        public Slider airQualitySlider;
        public TextMeshProUGUI dayText;
        public TextMeshProUGUI universityLevelText;

        // --- 核心全局变量 ---
        public int _currentDay;
        private float _money;
        public int _currentPopulation;
        private int _populationCapacity;
        private int _basePopulation;
        private int _employedPopulation;
        private float _food;
        private float _foodProductionRate; // 每秒生产的食物
        private float _baseElectricityProduction;
        private float _electricityProduction;
        public float _happiness;
        private float _baseCarbonDioxideEmission;
        private float _carbonDioxideEmission;
        public float _airQuality;
        private float _carbonDioxideAbsorption = 0f;
        private int _bankCount = 0;
        private int _universityLevel = 1;
        // <<< +++ 新增: 跟踪所有建筑的总耗电量 +++
        private float _totalElectricityConsumption = 0f;

        // <<< +++ 新增: 跟踪所有已放置的建筑实例 +++
        private List<BuildingEffect> _allPlacedBuildings = new List<BuildingEffect>();

        private Dictionary<BuildingType, int> _buildingCounts = new Dictionary<BuildingType, int>();

        // --- 新增科研相关变量 ---
        private float _powerEfficiencyModifier = 1.0f; // 电力效率修正，初始为100%
        private float _co2EmissionModifier = 1.0f;     // 碳排放修正，初始为100%

        // --- 游戏平衡性参数 (可以在编辑器中调整) ---
        [Header("Game Balance Settings")]
        public float startingMoney = 1000f;
        public int startingPopulationCapacity = 10;
        public float populationDecreaseRate = 0.2f;
        public float foodConsumptionPerPerson = 0.1f; // 每人每秒消耗的食物
        public float populationGrowthRate = 0.5f; // 当食物充足时，每秒人口增长的点数
        public float moneyMultiplierFromFood = 0.5f;

        [Header("Electricity & Environment")]
        // <<< --- 删除: electricityPerPerson 已被移除，因为现在耗电由建筑决定 ---
        // public float electricityPerPerson = 0.2f; 
        public float happinessChangeRate = 1f;    // 幸福度每秒变化的点数 (现在由空气质量触发)
        public float airQualityRecoveryRate = 0.1f; // 空气质量每秒自然恢复的点数
        public float airQualityDeclineRate = 0.2f;  // 每单位二氧化碳排放导致空气质量下降的速率

        [Header("Research Settings")]
        public float researchCostBase = 100f;
        public float researchCostMultiplier = 1.5f;
        public int researchLevelCap = 10;
        public float powerEfficiencyGain = 0.1f;
        public float co2EmissionReduction = 0.1f;

        private float _populationGrowthProgress = 0f;
        private float _populationDecreaseProgress = 0f;

        // --- 公开属性，用于其他脚本访问 ---
        public float Money => _money;
        public int UniversityLevel => _universityLevel;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
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
            _totalElectricityConsumption = 0f; // <<< +++ 新增: 初始化耗电量 +++

            _buildingCounts.Clear();
            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
            {
                _buildingCounts[type] = 0;
            }

            UpdateUI();

            // 每秒调用一次核心逻辑更新
            InvokeRepeating(nameof(Tick), 1f, 1f);
        }

        // 每秒执行一次的核心游戏逻辑
        private void Tick()
        {
            _currentDay++;

            // --- 重新计算应用增益后的总产量/排放 ---
            // <<< --- 修改: 电力生产 = 总消耗 (PowerPlant自动满足) ---
            _baseElectricityProduction = _totalElectricityConsumption;
            _electricityProduction = _baseElectricityProduction * _powerEfficiencyModifier;
            _carbonDioxideEmission = _baseCarbonDioxideEmission * _co2EmissionModifier;

            // 1. 生产食物
            _food += _foodProductionRate;

            // 2. 人口消耗食物
            float foodConsumed = _currentPopulation * foodConsumptionPerPerson;

            // 3. 判断食物是否充足
            if (_food >= foodConsumed)
            {
                // 食物充足的逻辑
                _food -= foodConsumed;

                // --- 新的银行逻辑 ---
                if (_bankCount > 0 && foodConsumed > 0)
                {
                    AddMoney(foodConsumed * moneyMultiplierFromFood);
                }

                // 人口增长
                if (_currentPopulation < _populationCapacity && (_currentPopulation > 0 || _foodProductionRate > 0))
                {
                    _populationGrowthProgress += populationGrowthRate;
                    if (_populationGrowthProgress >= 1f)
                    {
                        _currentPopulation++;
                        _populationGrowthProgress -= 1f;
                    }
                }
            }
            else
            {
                // 食物不足的逻辑 (饥饿)
                _food = 0;

                if (_currentPopulation > _basePopulation)
                {
                    _populationDecreaseProgress += populationDecreaseRate;
                    if (_populationDecreaseProgress >= 1f)
                    {
                        _currentPopulation--;
                        _populationDecreaseProgress -= 1f;
                    }
                }
            }

            // 4. 电力计算
            // <<< --- 修改: 移除旧的 per-person 消耗 ---
            // float electricityConsumed = _currentPopulation * electricityPerPerson;
            // <<< --- 消耗现在等于所有建筑的总和 (电力生产会自动匹配此值) ---
            float electricityConsumed = _totalElectricityConsumption;

            // 5. 幸福度计算
            // <<< --- 修改: 根据用户要求，电力总是被满足，不再影响幸福度 ---
            // <<< --- 替换: 幸福度现在与空气质量挂钩 ---
            if (_airQuality > 70) // 如果空气质量好
            {
                _happiness += happinessChangeRate;
            }
            else if (_airQuality < 40) // 如果空气质量差
            {
                _happiness -= happinessChangeRate * 1.5f; // 差空气导致幸福度下降更快
            }
            _happiness = Mathf.Clamp(_happiness, 0f, 100f);

            // 6. 空气质量计算
            // a. 计算基础值 (已在Tick开头完成)
            // _electricityProduction = _baseElectricityProduction * _powerEfficiencyModifier;
            // _carbonDioxideEmission = _baseCarbonDioxideEmission * _co2EmissionModifier;

            // b. 计算净排放量 (总排放 - 总吸收)
            float netEmission = _carbonDioxideEmission - _carbonDioxideAbsorption;

            // c. 因净排放而下降
            if (netEmission > 0)
            {
                _airQuality -= netEmission * airQualityDeclineRate;
            }

            // d. 自然恢复
            _airQuality += airQualityRecoveryRate;

            _airQuality = Mathf.Clamp(_airQuality, 0f, 100f);


            UpdateUI();
        }

        private void UpdateUI()
        {
            moneyText.text = $"Money: {_money:F0}";
            populationText.text = $"Population: {_currentPopulation} / {_populationCapacity}";
            foodText.text = $"Food: {_food:F0}";

            // <<< --- 修改: UI现在显示总消耗量 (需求) ---
            // float electricityBalance = _electricityProduction - (_currentPopulation * electricityPerPerson);
            electricityText.text = $"Electricity Demand: {_totalElectricityConsumption:F1}";

            happinessText.text = $"{_happiness:F0}%";
            if (happinessSlider != null)
            {
                happinessSlider.value = _happiness;
            }
            airQualityText.text = $"{_airQuality:F0}%";
            if (airQualitySlider != null)
            {
                airQualitySlider.value = _airQuality;
            }
            if (dayText != null)
            {
                dayText.text = $"Day {_currentDay}";
            }

            if (universityLevelText != null)
            {
                universityLevelText.text = $"University Level: {_universityLevel}";
            }
        }

        // --- 公共方法，供其他脚本调用 ---
        public bool SpendMoney(float amount)
        {
            if (_money >= amount)
            {
                _money -= amount;
                UpdateUI();
                return true;
            }
            return false;
        }

        public void AddMoney(float amount)
        {
            _money += amount;
            UpdateUI();
        }

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

        public void AddFoodProduction(float amount, int workersRequired)
        {
            if (GetUnemployedPopulation() >= workersRequired)
            {
                _employedPopulation += workersRequired;
                _foodProductionRate += amount;
                UpdateUI();
            }
            else
            {
                Debug.LogWarning("You need people to run the farm!");
            }
        }

        public void RemoveFoodProduction(float amount, int workersFreed)
        {
            _employedPopulation -= workersFreed;
            _foodProductionRate -= amount;
            if (_foodProductionRate < 0) _foodProductionRate = 0;
            UpdateUI();
        }

        // <<< --- 修改: 发电厂效果现在只接收 CO2 ---
        public void AddPowerPlantEffect(float co2)
        {
            _baseCarbonDioxideEmission += co2;
            UpdateUI();
        }

        // <<< --- 修改: 发电厂效果现在只移除 CO2 ---
        public void RemovePowerPlantEffect(float co2)
        {
            _baseCarbonDioxideEmission -= co2;
            // <<< --- 修正: 这里应该检查 _baseCarbonDioxideEmission ---
            if (_baseCarbonDioxideEmission < 0) _baseCarbonDioxideEmission = 0;
            UpdateUI();
        }

        // <<< --- FundResearch 方法保持不变 ---
        public void FundResearch()
        {
            if (_universityLevel >= researchLevelCap)
            {
                Debug.Log("University is already at MAX Level!");
                return;
            }

            float currentResearchCost = researchCostBase * Mathf.Pow(researchCostMultiplier, _universityLevel - 1);

            if (SpendMoney(currentResearchCost))
            {
                _universityLevel++;

                // !!! 注意: 你的研究系统当前只提升等级，
                // 并没有实际应用 _powerEfficiencyModifier 或 _co2EmissionModifier 的变化。
                // 你可能需要在这里添加如下逻辑:
                // _powerEfficiencyModifier += powerEfficiencyGain;
                // _co2EmissionModifier -= co2EmissionReduction;

                Debug.Log($"Research Succeed! University Level is now: {_universityLevel}");
                UpdateUI();
            }
            else
            {
                Debug.Log($"Not Enough Funding for Research! Need {currentResearchCost:F0}");
            }
        }

        public void AddCo2Absorption(float amount)
        {
            _carbonDioxideAbsorption += amount;
            UpdateUI();
        }

        public void RemoveCo2Absorption(float amount)
        {
            _carbonDioxideAbsorption -= amount;
            if (_carbonDioxideAbsorption < 0) _carbonDioxideAbsorption = 0;
            UpdateUI();
        }
        public void AddBank()
        {
            _bankCount++;
        }

        public void RemoveBank()
        {
            _bankCount--;
            if (_bankCount < 0) _bankCount = 0;
        }

        public float GetCurrentNetEmission()
        {
            // <<< --- 修改: 确保使用应用了修正的 _carbonDioxideEmission ---
            float netEmission = _carbonDioxideEmission - _carbonDioxideAbsorption;
            return (netEmission > 0) ? netEmission : 0;
        }

        public int GetUnemployedPopulation()
        {
            return _currentPopulation - _employedPopulation;
        }

        public void RegisterBuilding(BuildingType type)
        {
            if (_buildingCounts.ContainsKey(type))
            {
                _buildingCounts[type]++;
            }
        }

        public void UnregisterBuilding(BuildingType type)
        {
            if (_buildingCounts.ContainsKey(type))
            {
                _buildingCounts[type]--;
                if (_buildingCounts[type] < 0) _buildingCounts[type] = 0;
            }
        }


        public bool CanBuildBuilding(BuildingType type)
        {
            return _buildingCounts.ContainsKey(type) && _buildingCounts[type] == 0;
        }

        // <<< +++ 
        // +++ 新增: 用于建筑添加/移除耗电量
        // +++ 
        public void AddElectricityConsumption(float amount)
        {
            _totalElectricityConsumption += amount;
            UpdateUI(); // 更新UI以显示新的电力平衡
        }

        public void RemoveElectricityConsumption(float amount)
        {
            _totalElectricityConsumption -= amount;
            if (_totalElectricityConsumption < 0) _totalElectricityConsumption = 0;
            UpdateUI(); // 更新UI以显示新的电力平衡
        }

        /// <summary>
        /// 注册一个建筑实例到全局列表
        /// </summary>
        public void RegisterBuildingInstance(BuildingEffect building)
        {
            if (!_allPlacedBuildings.Contains(building))
            {
                _allPlacedBuildings.Add(building);
                Debug.Log($"Building instance registered. Total count: {_allPlacedBuildings.Count}");
            }
        }

        /// <summary>
        /// 从全局列表中注销一个建筑实例
        /// </summary>
        public void UnregisterBuildingInstance(BuildingEffect building)
        {
            if (_allPlacedBuildings.Contains(building))
            {
                _allPlacedBuildings.Remove(building);
                Debug.Log($"Building instance unregistered. Total count: {_allPlacedBuildings.Count}");
            }
        }

        /// <summary>
        /// 获取所有已放置建筑的列表 (返回一个副本以防止迭代时修改)
        /// </summary>
        public List<BuildingEffect> GetAllPlacedBuildings()
        {
            // 清理列表中可能已被摧毁的空引用
            _allPlacedBuildings.RemoveAll(item => item == null);
            // 返回一个新列表，这样遍历时删除元素也不会出错
            return new List<BuildingEffect>(_allPlacedBuildings);
        }
    }
}