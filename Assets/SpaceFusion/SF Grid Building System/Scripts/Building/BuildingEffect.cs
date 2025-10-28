using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public enum BuildingType { House, Farm, Institute, PowerPlant, Co2Storage, Bank }

    public class BuildingEffect : MonoBehaviour
    {
        public BuildingType type;

        [Header("Level Settings")]
        public int buildingLevel = 1; // <<< +++ 新增: 建筑当前等级 +++
        public int maxBuildingLevel = 10; // <<< +++ 新增: 建筑最高等级 +++
        // <<< +++ 新增: 升级成本数组 (索引0 = 升到2级的成本, 索引1 = 升到3级的成本... 共9个元素) +++
        public float[] upgradeCostPerLevel = { 50, 100, 180, 300, 500, 800, 1300, 2100, 3400 };

        [Header("House Settings")]
        // <<< --- 修改: 替换为等级数组 (请在Inspector中填充10个值) ---
        public int[] populationCapacityPerLevel = { 5, 8, 12, 18, 25, 35, 50, 70, 100, 140 };
        public int[] initialPopulationPerLevel = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

        [Header("Farm Settings")]
        // <<< --- 修改: 替换为等级数组 (请在Inspector中填充10个值) ---
        public float[] foodProductionPerLevel = { 2f, 3f, 4.5f, 6f, 8f, 10f, 12f, 14f, 16f, 18f };
        public int[] workersRequiredPerLevel = { 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 };

        [Header("PowerPlant Settings")]
        // <<< --- 修改: 替换为等级数组 (请在Inspector中填充10个值) ---
        public float[] electricityProductionPerLevel = { 10f, 15f, 22f, 30f, 40f, 55f, 70f, 90f, 115f, 150f };
        public float[] co2EmissionPerLevel = { 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f, 6.5f };

        [Header("Co2 Storage Settings")]
        // <<< --- 修改: 替换为等级数组 (请在Inspector中填充10个值) ---
        public float[] co2AbsorptionRatePerLevel = { 0.5f, 0.7f, 1f, 1.4f, 1.9f, 2.5f, 3.2f, 4f, 5f, 6f };
        public float[] co2CapacityPerLevel = { 100f, 130f, 170f, 220f, 280f, 350f, 440f, 550f, 700f, 900f };

        // --- 内部状态追踪变量 ---
        private float _currentCo2Stored = 0f;
        private bool _isStorageActive = false; // 用于控制Update逻辑是否执行
        // <<< +++ 新增: 用于存储当前等级的吸收率和容量，供Update使用 +++
        private float _currentAbsorptionRate = 0f;
        private float _currentCapacity = 0f;

        private void Update()
        {
            if (!_isStorageActive)
            {
                return;
            }

            // <<< --- 修改: 使用 _currentAbsorptionRate 变量 ---
            _currentCo2Stored += _currentAbsorptionRate * Time.deltaTime;

            // <<< --- 修改: 使用 _currentCapacity 变量 ---
            if (_currentCo2Stored >= _currentCapacity)
            {
                // <<< --- 修改: 使用 _currentCapacity 变量 ---
                _currentCo2Stored = _currentCapacity;
                _isStorageActive = false;

                // <<< --- 修改: 使用 _currentAbsorptionRate 变量 ---
                ResourceManager.Instance.RemoveCo2Absorption(_currentAbsorptionRate);

                Debug.Log("One Co2 Storage is Full!");
            }
        }

        // 当建筑被成功放置时调用 (或升级后调用)
        public void ApplyEffect()
        {
            // <<< +++ 新增: 获取当前等级对应的数组索引 +++
            int levelIndex = buildingLevel - 1;
            // (防止数组越界)
            if (levelIndex < 0) levelIndex = 0;

            switch (type)
            {
                case BuildingType.House:
                    // <<< --- 修改: 从数组中获取数据 ---
                    if (levelIndex < populationCapacityPerLevel.Length && levelIndex < initialPopulationPerLevel.Length)
                    {
                        ResourceManager.Instance.AddHouseEffect(
                            populationCapacityPerLevel[levelIndex],
                            initialPopulationPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Farm:
                    // <<< --- 修改: 从数组中获取数据 ---
                    if (levelIndex < foodProductionPerLevel.Length && levelIndex < workersRequiredPerLevel.Length)
                    {
                        ResourceManager.Instance.AddFoodProduction(
                            foodProductionPerLevel[levelIndex],
                            workersRequiredPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Bank:
                    ResourceManager.Instance.AddBank();
                    break;
                case BuildingType.PowerPlant:
                    // <<< --- 修改: 从数组中获取数据 ---
                    if (levelIndex < electricityProductionPerLevel.Length && levelIndex < co2EmissionPerLevel.Length)
                    {
                        ResourceManager.Instance.AddPowerPlantEffect(
                            electricityProductionPerLevel[levelIndex],
                            co2EmissionPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Co2Storage:
                    // <<< --- 修改: 从数组中获取数据并设置内部状态 ---
                    if (levelIndex < co2AbsorptionRatePerLevel.Length && levelIndex < co2CapacityPerLevel.Length)
                    {
                        _currentAbsorptionRate = co2AbsorptionRatePerLevel[levelIndex];
                        _currentCapacity = co2CapacityPerLevel[levelIndex];
                        _currentCo2Stored = 0f; // 升级或放置时清空
                        _isStorageActive = true;
                        ResourceManager.Instance.AddCo2Absorption(_currentAbsorptionRate);
                    }
                    break;
                case BuildingType.Institute:
                    break;
            }
        }

        // 当建筑被移除时调用 (或升级前调用)
        public void RemoveEffect()
        {
            // <<< +++ 新增: 获取当前等级对应的数组索引 +++
            int levelIndex = buildingLevel - 1;
            if (levelIndex < 0) levelIndex = 0;

            switch (type)
            {
                case BuildingType.House:
                    // <<< --- 修改: 从数组中获取数据 ---
                    if (levelIndex < populationCapacityPerLevel.Length && levelIndex < initialPopulationPerLevel.Length)
                    {
                        ResourceManager.Instance.RemoveHouseEffect(
                            populationCapacityPerLevel[levelIndex],
                            initialPopulationPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Farm:
                    // <<< --- 修改: 从数组中获取数据 ---
                    if (levelIndex < foodProductionPerLevel.Length && levelIndex < workersRequiredPerLevel.Length)
                    {
                        ResourceManager.Instance.RemoveFoodProduction(
                            foodProductionPerLevel[levelIndex],
                            workersRequiredPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Bank:
                    ResourceManager.Instance.RemoveBank();
                    break;
                case BuildingType.PowerPlant:
                    // <<< --- 修改: 从数组中获取数据 ---
                    if (levelIndex < electricityProductionPerLevel.Length && levelIndex < co2EmissionPerLevel.Length)
                    {
                        ResourceManager.Instance.RemovePowerPlantEffect(
                            electricityProductionPerLevel[levelIndex],
                            co2EmissionPerLevel[levelIndex]);
                    }
                    break;
                case BuildingType.Co2Storage:
                    // <<< --- 修改: 使用 _currentAbsorptionRate 变量 ---
                    if (_isStorageActive)
                    {
                        ResourceManager.Instance.RemoveCo2Absorption(_currentAbsorptionRate);
                    }
                    _isStorageActive = false;
                    _currentAbsorptionRate = 0; // 重置
                    _currentCapacity = 0; // 重置
                    break;
                case BuildingType.Institute:
                    break;
            }
        }

        // <<< +++ 
        // +++ 新增: 尝试升级建筑的公共方法
        // +++ 您需要从其他脚本 (例如建筑点击UI) 来调用这个方法
        // +++ 
        public void TryUpgradeBuilding()
        {
            // 检查1: 是否已达最高等级
            if (buildingLevel >= maxBuildingLevel)
            {
                Debug.Log($"Building '{gameObject.name}' is already at max level ({maxBuildingLevel}).");
                // 可以在此显示UI提示
                return;
            }

            // 检查2: 大学等级是否足够
            // 规则: 建筑的 *当前* 等级不能低于大学等级
            // (换句话说: 你必须先升到大学2级，才能把建筑升到2级)
            if (buildingLevel >= ResourceManager.Instance.UniversityLevel)
            {
                Debug.Log($"Cannot upgrade building. University Level {ResourceManager.Instance.UniversityLevel + 1} is required to upgrade to Level {buildingLevel + 1}.");
                // 可以在此显示UI提示
                return;
            }

            // 检查3: 检查升级成本
            // 成本数组索引 = (当前等级 - 1)
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

                // (您可以在这里添加其他逻辑，比如更换建筑模型、播放特效等)
            }
            else
            {
                Debug.Log($"Not enough money to upgrade. Need {cost:F0}.");
                // 可以在此显示UI提示
            }
        }
    }
}