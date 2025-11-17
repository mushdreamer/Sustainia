using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public enum BuildingType { House, Farm, Institute, PowerPlant, Co2Storage, Bank }

    public class BuildingEffect : MonoBehaviour
    {
        public BuildingType type;

        [Header("Level Settings")]
        public int buildingLevel = 1;
        public int maxBuildingLevel = 10;
        public float[] upgradeCostPerLevel = { 50, 100, 180, 300, 500, 800, 1300, 2100, 3400 };

        [Header("Health")]
        [Tooltip("这个建筑 Prefab 的最大血量 (应匹配 Placeable.cs 中的设置)")]
        public float maxHealth = 100f;
        private float _currentHealth;

        // <<< +++ 新增: 通用耗电设置 +++
        [Header("General Consumption")]
        // <<< +++ (请在Inspector中为 *除PowerPlant外* 的所有建筑Prefab填充这10个值) +++
        public float[] electricityConsumptionPerLevel = { 1f, 1.2f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f };

        [Header("House Settings")]
        public int[] populationCapacityPerLevel = { 5, 8, 12, 18, 25, 35, 50, 70, 100, 140 };
        public int[] initialPopulationPerLevel = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

        [Header("Farm Settings")]
        public float[] foodProductionPerLevel = { 2f, 3f, 4.5f, 6f, 8f, 10f, 12f, 14f, 16f, 18f };

        [Header("PowerPlant Settings")]
        // <<< --- 修改: 此数组已不再使用，因为电力是自动满足的 ---
        public float[] electricityProductionPerLevel = { 10f, 15f, 22f, 30f, 40f, 55f, 70f, 90f, 115f, 150f };
        public float[] co2EmissionPerLevel = { 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f, 6.5f };

        [Header("Co2 Storage Settings")]
        public float[] co2AbsorptionRatePerLevel = { 0.5f, 0.7f, 1f, 1.4f, 1.9f, 2.5f, 3.2f, 4f, 5f, 6f };
        public float[] co2CapacityPerLevel = { 100f, 130f, 170f, 220f, 280f, 350f, 440f, 550f, 700f, 900f };

        // --- 内部状态追踪变量 ---
        private bool _isInitialized = false;
        private float _currentCo2Stored = 0f;
        private bool _isStorageActive = false;
        private float _currentAbsorptionRate = 0f;
        private float _currentCapacity = 0f;
        // <<< +++ 新增: 存储当前耗电量，用于RemoveEffect +++
        private float _currentElectricityConsumption = 0f;

        private void Start()
        {
            // 对于预先放置在场景中的建筑 (不是由玩家或加载程序放置的)，
            // 它们也需要在游戏开始时应用其效果。
            // _isInitialized 标志会防止 PlacementHandler (在放置新物体时) 再次调用它。
            if (!_isInitialized)
            {
                ApplyEffect();
            }
        }

        private void Update()
        {
            if (!_isStorageActive)
            {
                return;
            }

            _currentCo2Stored += _currentAbsorptionRate * Time.deltaTime;

            if (_currentCo2Stored >= _currentCapacity)
            {
                _currentCo2Stored = _currentCapacity;
                _isStorageActive = false;

                ResourceManager.Instance.RemoveCo2Absorption(_currentAbsorptionRate);

                Debug.Log("One Co2 Storage is Full!");
            }
        }

        // 当建筑被成功放置时调用 (或升级后调用)
        public void ApplyEffect()
        {
            // <<< +++ 新增: 向 ResourceManager 注册实例 +++
            ResourceManager.Instance.RegisterBuildingInstance(this);
            // <<< +++ 新增: 初始化血量 +++
            _currentHealth = maxHealth;

            ResourceManager.Instance.RegisterBuilding(type);

            int levelIndex = buildingLevel - 1;
            if (levelIndex < 0) levelIndex = 0;

            // <<< +++ 新增: (几乎)所有建筑都会耗电 +++
            if (type != BuildingType.PowerPlant && levelIndex < electricityConsumptionPerLevel.Length)
            {
                _currentElectricityConsumption = electricityConsumptionPerLevel[levelIndex];
                ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
            }
            // <<< +++ --------------------------- +++

            switch (type)
            {
                case BuildingType.House:
                    if (levelIndex < populationCapacityPerLevel.Length && levelIndex < initialPopulationPerLevel.Length)
                    {
                        ResourceManager.Instance.AddHouseEffect(
                            populationCapacityPerLevel[levelIndex],
                            initialPopulationPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Farm:
                    if (levelIndex < foodProductionPerLevel.Length)
                    {
                        ResourceManager.Instance.AddFoodProduction(
                            foodProductionPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Bank:
                    ResourceManager.Instance.AddBank();
                    break;
                case BuildingType.PowerPlant:
                    // <<< --- 修改: 发电厂现在只应用CO2排放 ---
                    // <<< --- 电力生产是自动的，不再需要 electricityProductionPerLevel ---
                    if (levelIndex < co2EmissionPerLevel.Length)
                    {
                        ResourceManager.Instance.AddPowerPlantEffect(
                            co2EmissionPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Co2Storage:
                    if (levelIndex < co2AbsorptionRatePerLevel.Length && levelIndex < co2CapacityPerLevel.Length)
                    {
                        _currentAbsorptionRate = co2AbsorptionRatePerLevel[levelIndex];
                        _currentCapacity = co2CapacityPerLevel[levelIndex];
                        _currentCo2Stored = 0f;
                        _isStorageActive = true;
                        ResourceManager.Instance.AddCo2Absorption(_currentAbsorptionRate);
                    }
                    break;
                case BuildingType.Institute:
                    // (研究所现在也会耗电，已在上面处理)
                    break;
            }
        }

        // 当建筑被移除时调用 (或升级前调用)
        public void RemoveEffect()
        {
            // <<< +++ 新增: 向 ResourceManager 注销实例 +++
            ResourceManager.Instance.UnregisterBuildingInstance(this);

            ResourceManager.Instance.UnregisterBuilding(type);

            int levelIndex = buildingLevel - 1;
            if (levelIndex < 0) levelIndex = 0;

            // <<< +++ 新增: 移除建筑时，也移除其耗电量 +++
            if (type != BuildingType.PowerPlant)
            {
                ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                _currentElectricityConsumption = 0; // 重置
            }
            // <<< +++ --------------------------- +++

            switch (type)
            {
                case BuildingType.House:
                    if (levelIndex < populationCapacityPerLevel.Length && levelIndex < initialPopulationPerLevel.Length)
                    {
                        ResourceManager.Instance.RemoveHouseEffect(
                            populationCapacityPerLevel[levelIndex],
                            initialPopulationPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Farm:
                    if (levelIndex < foodProductionPerLevel.Length)
                    {
                        ResourceManager.Instance.RemoveFoodProduction(
                            foodProductionPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Bank:
                    ResourceManager.Instance.RemoveBank();
                    break;
                case BuildingType.PowerPlant:
                    // <<< --- 修改: 只移除CO2排放 ---
                    // (我们使用 buildingLevel-1 来获取 *当前* 等级的索引，因为这是 Remove)
                    if (levelIndex < co2EmissionPerLevel.Length)
                    {
                        ResourceManager.Instance.RemovePowerPlantEffect(
                            co2EmissionPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Co2Storage:
                    if (_isStorageActive)
                    {
                        ResourceManager.Instance.RemoveCo2Absorption(_currentAbsorptionRate);
                    }
                    _isStorageActive = false;
                    _currentAbsorptionRate = 0;
                    _currentCapacity = 0;
                    break;
                case BuildingType.Institute:
                    // (耗电量已在上面移除)
                    break;
            }
        }

        // ... TryUpgradeBuilding 方法保持不变 (它会正确调用 RemoveEffect 和 ApplyEffect)
        public void TryUpgradeBuilding()
        {
            // 检查1: 是否已达最高等级
            if (buildingLevel >= maxBuildingLevel)
            {
                Debug.Log($"Building '{gameObject.name}' is already at max level ({maxBuildingLevel}).");
                return;
            }

            // 检查2: 大学等级是否足够
            if (buildingLevel >= ResourceManager.Instance.UniversityLevel)
            {
                Debug.Log($"Cannot upgrade building. University Level {ResourceManager.Instance.UniversityLevel + 1} is required to upgrade to Level {buildingLevel + 1}.");
                return;
            }

            // 检查3: 检查升级成本
            int costIndex = buildingLevel - 1;
            if (costIndex >= upgradeCostPerLevel.Length)
            {
                Debug.LogError($"Missing upgrade cost data for Level {buildingLevel + 1} on building '{gameObject.name}'.");
                return;
            }

            float cost = upgradeCostPerLevel[costIndex];

            // 检查4: 钱是否足够
            if (ResourceManager.Instance.SpendMoney(cost))
            {
                // 升级成功
                // 1. 移除旧等级的效果
                RemoveEffect();

                // 2. 提升等级
                buildingLevel++;
                Debug.Log($"Building '{gameObject.name}' upgraded to Level {buildingLevel}!");

                // 3. 应用新等级的效果
                ApplyEffect();
            }
            else
            {
                Debug.Log($"Not enough money to upgrade. Need {cost:F0}.");
            }
        }
        // <<< +++ 
        // +++ 新增: 伤害和摧毁逻辑
        // +++ 
        /// <summary>
        /// 对这个建筑造成伤害
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_currentHealth <= 0) return; // 已经被摧毁了

            _currentHealth -= amount;
            Debug.Log($"Building '{gameObject.name}' took {amount} damage. Current health: {_currentHealth}/{maxHealth}");

            if (_currentHealth <= 0)
            {
                _currentHealth = 0;
                DestroyBuilding();
            }
        }

        /// <summary>
        /// 建筑血量归零时调用
        /// </summary>
        private void DestroyBuilding()
        {
            Debug.Log($"Building '{gameObject.name}' has been destroyed by damage!");

            // 1. 获取挂载在同一个 GameObject 上的 PlacedObject 组件
            PlacedObject placedObject = GetComponent<PlacedObject>();

            if (placedObject != null && PlacementSystem.Instance != null)
            {
                // 2. 调用 PlacementSystem 的官方 "Remove" 功能
                //    这会触发 RemoveState，然后正确调用 PlacementHandler，
                //    它会为你处理 *所有* 事情：
                //      - 注销独特建筑 (修复你的 BUG 2)
                //      - 从 GridData 释放格子 (修复你的 BUG 1)
                //      - 调用 RemoveEffect() (你的代码已包含)
                //      - 从存档中移除
                //      - 最后才 Destroy(gameObject)
                PlacementSystem.Instance.Remove(placedObject);
            }
            else
            {
                // 备用方案: 如果找不到 PlacementSystem 或 PlacedObject，
                // 至少执行旧的逻辑，防止 GameObject 留在原地。
                Debug.LogError($"无法通过 PlacementSystem 移除 {gameObject.name}！执行紧急销毁。");
                RemoveEffect();
                Destroy(gameObject);
            }
        }
        // <<< +++ ---------------------------------- +++
    }
}