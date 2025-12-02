using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public enum BuildingType { House, Farm, Institute, PowerPlant, Co2Storage, Bank }

    public class BuildingEffect : MonoBehaviour
    {
        public BuildingType type;

        [Header("Health")]
        public float maxHealth = 100f;
        private float _currentHealth;

        [Header("General Consumption")]
        public float electricityConsumption = 1f;

        // --- 各个建筑独立的 Co2 设置 (正数=排放, 负数=吸收) ---

        [Header("House Settings")]
        public int populationCapacity = 5;
        public int initialPopulation = 2;
        [Tooltip("正数表示排放 Co2")]
        public float houseCo2Change = 1f;

        [Header("Farm Settings")]
        public float foodProduction = 2f;
        [Tooltip("正数表示排放 Co2")]
        public float farmCo2Change = 2f;

        [Header("Institute Settings")]
        [Tooltip("正数表示排放 Co2")]
        public float instituteCo2Change = 3f;

        [Header("Bank Settings")]
        [Tooltip("正数表示排放 Co2")]
        public float bankCo2Change = 1.5f;

        [Header("PowerPlant Settings")]
        [Tooltip("正数表示排放 Co2")]
        public float powerPlantCo2Change = 10f;

        [Header("Co2 Storage Settings")]
        [Tooltip("负数表示吸收/减少 Co2")]
        public float storageCo2Change = -20f; // <<< 默认为负数，表示吸收

        // --- 内部状态变量 ---
        private bool _isInitialized = false;

        // 运行时缓存
        private float _currentElectricityConsumption = 0f;

        // 这个变量现在存储“净 Co2 变化量”，可能是正数也可能是负数
        private float _currentCo2Change = 0f;

        private float _currentFoodProduction = 0f;
        private int _currentPopCapacity = 0;
        private int _currentPopInitial = 0;

        private void Start()
        {
            if (!_isInitialized)
            {
                ApplyEffect();
            }
        }

        public void ApplyEffect()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            ResourceManager.Instance.RegisterBuildingInstance(this);
            _currentHealth = maxHealth;
            ResourceManager.Instance.RegisterBuilding(type);

            // 1. 通用耗电 (PowerPlant 除外)
            if (type != BuildingType.PowerPlant)
            {
                _currentElectricityConsumption = electricityConsumption;
                ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
            }

            // 2. 确定当前建筑的 Co2 变化值 (正/负)
            switch (type)
            {
                case BuildingType.House:
                    _currentCo2Change = houseCo2Change;
                    _currentPopCapacity = populationCapacity;
                    _currentPopInitial = initialPopulation;
                    ResourceManager.Instance.AddHouseEffect(_currentPopCapacity, _currentPopInitial);
                    break;

                case BuildingType.Farm:
                    _currentCo2Change = farmCo2Change;
                    _currentFoodProduction = foodProduction;
                    ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
                    break;

                case BuildingType.Institute:
                    _currentCo2Change = instituteCo2Change;
                    break;

                case BuildingType.Bank:
                    _currentCo2Change = bankCo2Change;
                    break;

                case BuildingType.PowerPlant:
                    _currentCo2Change = powerPlantCo2Change;
                    break;

                case BuildingType.Co2Storage:
                    // 直接读取负值配置，不再处理 Capacity 或 Rate
                    _currentCo2Change = storageCo2Change;
                    break;
            }

            // 3. 将数值应用到 ResourceManager
            // 无论正负，统一调用 AddPowerPlantEffect (或者你可以重命名为 AddCo2Effect)
            // 如果是负数，ResourceManager 那边的总 Co2 就会减少
            if (_currentCo2Change != 0)
            {
                ResourceManager.Instance.AddPowerPlantEffect(_currentCo2Change);
            }
        }

        public void RemoveEffect()
        {
            ResourceManager.Instance.UnregisterBuildingInstance(this);
            ResourceManager.Instance.UnregisterBuilding(type);

            // 1. 移除耗电
            if (type != BuildingType.PowerPlant)
            {
                ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                _currentElectricityConsumption = 0;
            }

            // 2. 移除 Co2 影响
            // 逻辑: 调用 Remove 传入原数值，ResourceManager 应该做减法
            // (例如: 原本是 -20，Remove(-20) => 总量减去-20 => 总量+20，恢复原状)
            if (_currentCo2Change != 0)
            {
                ResourceManager.Instance.RemovePowerPlantEffect(_currentCo2Change);
                _currentCo2Change = 0;
            }

            // 3. 移除其他特定效果
            switch (type)
            {
                case BuildingType.House:
                    ResourceManager.Instance.RemoveHouseEffect(_currentPopCapacity, _currentPopInitial);
                    break;
                case BuildingType.Farm:
                    ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
                    break;
                case BuildingType.Bank:
                    ResourceManager.Instance.RemoveBank();
                    break;
                    // Co2Storage 和 PowerPlant 的 Co2 逻辑已经在上面第2步统一处理了
            }
        }

        /// <summary>
        /// 动态修改 Co2 数值 (运行时升级或 Buff)
        /// </summary>
        public void UpdateCo2Change(float newValue)
        {
            // 1. 移除旧值的影响
            ResourceManager.Instance.RemovePowerPlantEffect(_currentCo2Change);

            // 2. 更新数值
            _currentCo2Change = newValue;

            // 3. 应用新值
            ResourceManager.Instance.AddPowerPlantEffect(_currentCo2Change);

            Debug.Log($"{gameObject.name} Co2 固定数值调整为: {newValue}");
        }

        public void TakeDamage(float amount)
        {
            if (_currentHealth <= 0) return;
            _currentHealth -= amount;
            if (_currentHealth <= 0)
            {
                _currentHealth = 0;
                DestroyBuilding();
            }
        }

        public void UpdateElectricityConsumption(float newValue)
        {
            if (type == BuildingType.PowerPlant) return;
            ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
            _currentElectricityConsumption = newValue;
            ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
        }

        public void UpdateFoodProduction(float newValue)
        {
            if (type != BuildingType.Farm) return;
            ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
            _currentFoodProduction = newValue;
            ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
        }

        public float GetCurrentElectricity() => _currentElectricityConsumption;
        public float GetCurrentFood() => _currentFoodProduction;

        // 这个方法返回当前的 Co2 影响值 (可能是正数也可能是负数)
        public float GetCurrentCo2Change() => _currentCo2Change;

        private void DestroyBuilding()
        {
            PlacedObject placedObject = GetComponent<PlacedObject>();
            if (placedObject != null && PlacementSystem.Instance != null)
            {
                PlacementSystem.Instance.Remove(placedObject);
            }
            else
            {
                RemoveEffect();
                Destroy(gameObject);
            }
        }
    }
}