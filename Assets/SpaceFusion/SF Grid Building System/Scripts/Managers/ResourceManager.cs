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

        // --- 核心全局变量 ---
        private float _money;
        private int _currentPopulation;
        private int _populationCapacity;
        private int _basePopulation;
        private int _employedPopulation;
        private float _food;
        private float _foodProductionRate; // 每秒生产的食物
        private float _baseElectricityProduction;      // <<< --- 修改: 电力基础产量 ---
        private float _electricityProduction;
        private float _happiness;
        private float _baseCarbonDioxideEmission;      // <<< --- 修改: 二氧化碳基础排放量 ---
        private float _carbonDioxideEmission;
        private float _airQuality;
        private bool _bankExists = false;

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

        [Header("Electricity & Environment")] // <<< --- 新增参数组 ---
        public float electricityPerPerson = 0.2f; // 每人每秒消耗的电力
        public float happinessChangeRate = 1f;    // 幸福度每秒变化的点数
        public float airQualityRecoveryRate = 0.1f; // 空气质量每秒自然恢复的点数
        public float airQualityDeclineRate = 0.2f;  // 每单位二氧化碳排放导致空气质量下降的速率

        [Header("Institute Settings")] // <<< --- 新增参数组 ---
        public float researchCost = 100f; // 每次科研投入的成本
        public float powerEfficiencyGain = 0.1f; // 每次科研提升的发电效率 (10%)
        public float co2EmissionReduction = 0.1f; // 每次科研降低的碳排放 (10%)

        private float _populationGrowthProgress = 0f;
        private float _populationDecreaseProgress = 0f;

        // --- 公开属性，用于其他脚本访问 ---
        public float Money => _money;

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
            _money = startingMoney;
            _populationCapacity = startingPopulationCapacity;
            _happiness = 100f;
            _airQuality = 100f;
            UpdateUI();

            // 每秒调用一次核心逻辑更新
            InvokeRepeating(nameof(Tick), 1f, 1f);
        }

        // 每秒执行一次的核心游戏逻辑
        private void Tick()
        {
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
                // 只有在人口成功消耗食物后，银行才会产生收入
                if (_bankExists && foodConsumed > 0)
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

                // --- 新的人口减少逻辑 ---
                // 只有在当前人口大于基础人口时，才会因饥饿减少
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
                // 电力充足，幸福度缓慢上升
                _happiness += happinessChangeRate;
                if (_happiness > 100f) _happiness = 100f;
            }
            else
            {
                // 电力不足，幸福度缓慢下降
                _happiness -= happinessChangeRate;
                if (_happiness < 0f) _happiness = 0f;
            }

            // 6. 空气质量计算
            // a. 因排放而下降
            _airQuality -= _carbonDioxideEmission * airQualityDeclineRate;
            // b. 自然恢复
            _airQuality += airQualityRecoveryRate;

            // 确保空气质量在0-100之间
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
                // 我们将在编辑器中把Slider的最大值设为100
                happinessSlider.value = _happiness; // <<< --- 添加这一行 ---
            }
            airQualityText.text = $"AirQ: {_airQuality:F0}%";
            if (airQualitySlider != null)
            {
                // 我们将在编辑器中把Slider的最大值设为100
                airQualitySlider.value = _airQuality; // <<< --- 添加这一行 ---
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

            // --- 关键逻辑：确保增加初始人口后，当前人口不会超过新的上限 ---
            _currentPopulation = Mathf.Min(_currentPopulation, _populationCapacity);

            UpdateUI();
        }

        public void RemoveHouseEffect(int capacityDecrease, int initialPopulationDecrease)
        {
            _populationCapacity -= capacityDecrease;
            _basePopulation -= initialPopulationDecrease;

            if (_basePopulation < 0) _basePopulation = 0;

            // --- 关键逻辑：移除房屋后，如果当前人口超过了新的上限，则将当前人口降低到与上限齐平 ---
            _currentPopulation = Mathf.Min(_currentPopulation, _populationCapacity);

            UpdateUI();
        }

        public void AddFoodProduction(float amount, int workersRequired)
        {
            // 检查是否有足够的人口来工作
            if (GetUnemployedPopulation() >= workersRequired)
            {
                _employedPopulation += workersRequired;
                _foodProductionRate += amount;
                UpdateUI();
            }
            else
            {
                Debug.LogWarning("没有足够的人口来农场工作!");
                // 这里可以提示玩家人口不足
            }
        }

        public void RemoveFoodProduction(float amount, int workersFreed)
        {
            _employedPopulation -= workersFreed;
            _foodProductionRate -= amount;
            if (_foodProductionRate < 0) _foodProductionRate = 0;
            UpdateUI();
        }

        // --- 新增公共方法 ---
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
        public void FundResearch()
        {
            if (SpendMoney(researchCost))
            {
                // 科研成功，应用增益
                _powerEfficiencyModifier += powerEfficiencyGain;
                _co2EmissionModifier -= co2EmissionReduction;

                // 确保排放修正不会低于0
                if (_co2EmissionModifier < 0) _co2EmissionModifier = 0;

                Debug.Log($"Research Succeed! Current Power Efficient: {_powerEfficiencyModifier * 100:F0}%, Co2 Emission: {_co2EmissionModifier * 100:F0}%");
                UpdateUI();
            }
            else
            {
                Debug.Log("Not Enough Funding for Research!");
                // 可以在此添加UI提示
            }
        }

        public void SetBankStatus(bool exists)
        {
            _bankExists = exists;
        }

        public int GetUnemployedPopulation()
        {
            return _currentPopulation - _employedPopulation;
        }
    }
}