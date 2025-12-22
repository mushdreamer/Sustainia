using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    // 确保枚举包含所有类型
    public enum BuildingType { House, Farm, Institute, PowerPlant, Co2Storage, Bank }

    public class BuildingEffect : MonoBehaviour
    {
        public BuildingType type;

        [Header("Health & Combat")]
        public float maxHealth = 100f;
        [SerializeField] // 方便在Inspector里看血量调试
        private float _currentHealth;

        [Header("General Consumption")]
        [Tooltip("基础耗电量 (正数=耗电)")]
        public float electricityConsumption = 1f;
        private float _currentElectricityConsumption;

        // --- 各个建筑的具体数值设置 ---

        [Header("House Settings")]
        public int populationCapacity = 5;
        public int initialPopulation = 2;
        [Tooltip("正数表示排放 Co2")]
        public float houseCo2Change = 1f;

        [Header("Farm Settings")]
        public float foodProduction = 2f;
        private float _currentFoodProduction;
        [Tooltip("正数表示排放 Co2")]
        public float farmCo2Change = 2f;

        [Header("Institute Settings")]
        [Tooltip("正数表示排放 Co2")]
        public float instituteCo2Change = 3f;

        [Header("Bank Settings")]
        [Tooltip("正数表示排放 Co2")]
        public float bankCo2Change = 1.5f;

        // --- Q13 教学相关设置 (新增) ---

        [Header("PowerPlant Settings (Emitter)")]
        [Tooltip("发电量 (将被转换为负耗电)")]
        public float powerProduction = 20f;
        [Tooltip("CO2 排放量")]
        public float powerPlantCo2Change = 10f;

        [Header("Co2Storage Settings (Absorber)")]
        [Tooltip("CCUS 耗电量")]
        public float storageConsumption = 5f;
        [Tooltip("CO2 吸收量 (正数)")]
        public float storageCo2Change = 8f;

        // 运行时记录当前的 CO2 影响 (用于统计)
        private float _currentCo2Change = 0f;

        private void Start()
        {
            if (ResourceManager.Instance == null) return;

            // 初始化血量 (之前漏掉的)
            _currentHealth = maxHealth;

            _currentElectricityConsumption = electricityConsumption;
            _currentFoodProduction = foodProduction;

            ApplyEffect();

            // 注册到全局列表
            ResourceManager.Instance.RegisterBuildingInstance(this);
            ResourceManager.Instance.RegisterBuilding(type);
        }

        private void OnDestroy()
        {
            if (ResourceManager.Instance == null) return;

            RemoveEffect();

            ResourceManager.Instance.UnregisterBuildingInstance(this);
            ResourceManager.Instance.UnregisterBuilding(type);
        }

        // --- 核心：应用效果 (合并了原有逻辑和Q13逻辑) ---
        public void ApplyEffect()
        {
            switch (type)
            {
                case BuildingType.House:
                    ResourceManager.Instance.AddHouseEffect(populationCapacity, initialPopulation);
                    ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(houseCo2Change);
                    _currentCo2Change = houseCo2Change;
                    break;

                case BuildingType.Farm:
                    ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
                    ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(farmCo2Change);
                    _currentCo2Change = farmCo2Change;
                    break;

                case BuildingType.Institute:
                    ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(instituteCo2Change);
                    _currentCo2Change = instituteCo2Change;
                    break;

                case BuildingType.Bank:
                    ResourceManager.Instance.AddBank();
                    ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(bankCo2Change);
                    _currentCo2Change = bankCo2Change;
                    break;

                case BuildingType.PowerPlant:
                    // Q13: 产生大量 CO2
                    ResourceManager.Instance.AddPowerPlantEffect(powerPlantCo2Change);
                    _currentCo2Change = powerPlantCo2Change;
                    // 产生电力 (负消耗)
                    _currentElectricityConsumption = -powerProduction;
                    ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
                    break;

                case BuildingType.Co2Storage:
                    // Q13: 吸收 CO2
                    ResourceManager.Instance.AddCo2Absorption(storageCo2Change);
                    _currentCo2Change = -storageCo2Change;
                    // 消耗电力
                    _currentElectricityConsumption = storageConsumption;
                    ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
                    break;

                default:
                    ResourceManager.Instance.AddElectricityConsumption(_currentElectricityConsumption);
                    break;
            }
        }

        // --- 核心：移除效果 ---
        public void RemoveEffect()
        {
            switch (type)
            {
                case BuildingType.House:
                    ResourceManager.Instance.RemoveHouseEffect(populationCapacity, initialPopulation);
                    ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(houseCo2Change);
                    break;

                case BuildingType.Farm:
                    ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
                    ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(farmCo2Change);
                    break;

                case BuildingType.Institute:
                    ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(instituteCo2Change);
                    break;

                case BuildingType.Bank:
                    ResourceManager.Instance.RemoveBank();
                    ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(bankCo2Change);
                    break;

                case BuildingType.PowerPlant:
                    ResourceManager.Instance.RemovePowerPlantEffect(powerPlantCo2Change);
                    ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                    break;

                case BuildingType.Co2Storage:
                    ResourceManager.Instance.RemoveCo2Absorption(storageCo2Change);
                    ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                    break;

                default:
                    ResourceManager.Instance.RemoveElectricityConsumption(_currentElectricityConsumption);
                    break;
            }
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;
            // 如果需要调试可以取消注释
            // Debug.Log($"{gameObject.name} took {damage} damage. Current HP: {_currentHealth}");

            if (_currentHealth <= 0)
            {
                DestroyBuilding();
            }
        }

        /// <summary>
        /// 治疗/修复
        /// </summary>
        public void Heal(float amount)
        {
            _currentHealth += amount;
            if (_currentHealth > maxHealth) _currentHealth = maxHealth;
        }

        // --- 其他工具方法 ---

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
        public float GetCurrentCo2Change() => _currentCo2Change;

        public void DestroyBuilding()
        {
            PlacedObject placedObject = GetComponent<PlacedObject>();
            if (placedObject != null && PlacementSystem.Instance != null)
            {
                // 通过系统移除，保证 GridData 清理
                PlacementSystem.Instance.Remove(placedObject);
            }
            else
            {
                // 强制销毁
                RemoveEffect();
                Destroy(gameObject);
            }
        }
    }
}