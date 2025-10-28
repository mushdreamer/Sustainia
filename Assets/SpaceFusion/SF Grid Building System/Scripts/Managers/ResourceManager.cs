using UnityEngine;
using TMPro; // 用于UI显示
using UnityEngine.UI;

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
        public TextMeshProUGUI universityLevelText; // <<< +++ 新增: 用于显示大学等级 +++

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
        private int _universityLevel = 1; // <<< +++ 新增: 全局大学/科研等级，初始为1 +++

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
        public float electricityPerPerson = 0.2f; // 每人每秒消耗的电力
        public float happinessChangeRate = 1f;    // 幸福度每秒变化的点数
        public float airQualityRecoveryRate = 0.1f; // 空气质量每秒自然恢复的点数
        public float airQualityDeclineRate = 0.2f;  // 每单位二氧化碳排放导致空气质量下降的速率

        // <<< --- 修改: "Institute Settings" 已重命名并扩展为 "Research Settings" ---
        [Header("Research Settings")]
        // <<< --- 修改: researchCost 重命名为 researchCostBase ---
        public float researchCostBase = 100f; // <<< +++ 每次科研投入的 基础 成本 +++
        public float researchCostMultiplier = 1.5f; // <<< +++ 新增: 科研成本的增长乘数 (例如 100, 150, 225...) +++
        public int researchLevelCap = 10; // <<< +++ 新增: 大学最高等级 +++
        public float powerEfficiencyGain = 0.1f; // 每次科研提升的发电效率 (10%)
        public float co2EmissionReduction = 0.1f; // 每次科研降低的碳排放 (10%)

        private float _populationGrowthProgress = 0f;
        private float _populationDecreaseProgress = 0f;

        // --- 公开属性，用于其他脚本访问 ---
        public float Money => _money;
        public int UniversityLevel => _universityLevel; // <<< +++ 新增: 公开访问大学等级 +++

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
            _universityLevel = 1; // <<< +++ 确保开始时为1级 +++
            UpdateUI();

            // 每秒调用一次核心逻辑更新
            InvokeRepeating(nameof(Tick), 1f, 1f);
        }

        // 每秒执行一次的核心游戏逻辑
        private void Tick()
        {
            _currentDay++;

            // --- 重新计算应用增益后的总产量/排放 ---
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
                _food = 0; // 耗尽所有食物

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
            float electricityConsumed = _currentPopulation * electricityPerPerson;

            // 5. 幸福度计算
            if (_electricityProduction >= electricityConsumed)
            {
                _happiness += happinessChangeRate;
                if (_happiness > 100f) _happiness = 100f;
            }
            else
            {
                _happiness -= happinessChangeRate;
                if (_happiness < 0f) _happiness = 0f;
            }

            // 6. 空气质量计算
            // a. 计算基础值
            _electricityProduction = _baseElectricityProduction * _powerEfficiencyModifier;
            _carbonDioxideEmission = _baseCarbonDioxideEmission * _co2EmissionModifier;

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
            float electricityBalance = _electricityProduction - (_currentPopulation * electricityPerPerson);
            electricityText.text = $"Electricity: {electricityBalance:F1}";
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
            // <<< +++ 新增: 更新大学等级UI +++
            if (universityLevelText != null)
            {
                universityLevelText.text = $"University Level: {_universityLevel}";
            }
            // <<< +++ ---------------------- +++
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

        public void AddPowerPlantEffect(float electricity, float co2)
        {
            _baseElectricityProduction += electricity;
            _baseCarbonDioxideEmission += co2;
            UpdateUI();
        }

        public void RemovePowerPlantEffect(float electricity, float co2)
        {
            _baseElectricityProduction -= electricity;
            _baseCarbonDioxideEmission -= co2;
            if (_electricityProduction < 0) _electricityProduction = 0;
            if (_carbonDioxideEmission < 0) _carbonDioxideEmission = 0;
            UpdateUI();
        }

        // <<< --- 修改: FundResearch 方法已更新 ---
        public void FundResearch()
        {
            // <<< +++ 新增: 检查是否已达最高等级 +++
            if (_universityLevel >= researchLevelCap)
            {
                Debug.Log("University is already at MAX Level!");
                return;
            }

            // <<< +++ 新增: 动态计算科研成本 +++
            // 成本 = 基础成本 * (乘数 ^ (当前等级 - 1))
            float currentResearchCost = researchCostBase * Mathf.Pow(researchCostMultiplier, _universityLevel - 1);

            // <<< --- 修改: 使用动态成本 ---
            if (SpendMoney(currentResearchCost))
            {
                // <<< +++ 新增: 提升大学等级 +++
                _universityLevel++;

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
            float netEmission = _carbonDioxideEmission - _carbonDioxideAbsorption;
            return (netEmission > 0) ? netEmission : 0;
        }

        public int GetUnemployedPopulation()
        {
            return _currentPopulation - _employedPopulation;
        }
    }
}