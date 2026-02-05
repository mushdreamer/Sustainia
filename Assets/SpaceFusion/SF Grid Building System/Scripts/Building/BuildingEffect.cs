using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;
using UnityEngine;

namespace SpaceFusion.SF_Grid_Building_System.Scripts.Core
{
    public enum BuildingType { House, Farm, Institute, PowerPlant, Co2Storage, Bank }

    public class BuildingEffect : MonoBehaviour
    {
        public BuildingType type;

        [Header("Health & Combat")]
        public float maxHealth = 100f;
        [SerializeField]
        private float _currentHealth;

        [Header("General Consumption")]
        public float electricityConsumption = 1f;
        private float _currentElectricityConsumption;

        [Header("House Settings")]
        public int populationCapacity = 5;
        public int initialPopulation = 2;
        public float houseCo2Change = 1f;

        [Header("Farm Settings")]
        public float foodProduction = 2f;
        private float _currentFoodProduction;
        public float farmCo2Change = 2f;

        [Header("Institute Settings")]
        public float instituteCo2Change = 3f;

        [Header("Bank Settings")]
        public float bankCo2Change = 1.5f;

        [Header("PowerPlant Settings (Emitter)")]
        public float powerProduction = 20f;
        public float powerPlantCo2Change = 10f;

        [Header("Co2Storage Settings (Absorber)")]
        public float storageConsumption = 5f;
        public float storageCo2Change = 8f;

        private float _currentCo2Change = 0f;
        private float _currentPowerProduction = 0f;

        private bool _isActive = false;

        private void Start()
        {
            if (ResourceManager.Instance == null) return;

            _currentHealth = maxHealth;
            _currentElectricityConsumption = electricityConsumption;
            _currentFoodProduction = foodProduction;

            if (type == BuildingType.PowerPlant)
            {
                _currentPowerProduction = powerProduction;
            }

            ApplyEffect();
        }

        private void OnDestroy()
        {
            if (_isActive && ResourceManager.Instance != null)
            {
                RemoveEffect();
                ResourceManager.Instance.UnregisterBuildingInstance(this);
                ResourceManager.Instance.UnregisterBuilding(type);
            }
        }

        public void ApplyEffect()
        {
            if (_isActive) return;
            _isActive = true;

            if (ResourceManager.Instance == null) return;

            Debug.Log($"[BuildingEffect] 激活建筑: {gameObject.name}, 类型: {type}, 发电量设置: {_currentPowerProduction}");

            ResourceManager.Instance.RegisterBuildingInstance(this);
            ResourceManager.Instance.RegisterBuilding(type);

            switch (type)
            {
                case BuildingType.House:
                    ResourceManager.Instance.AddHouseEffect(populationCapacity, initialPopulation);
                    ResourceManager.Instance.AddConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(houseCo2Change);
                    _currentCo2Change = houseCo2Change;
                    break;

                case BuildingType.Farm:
                    ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
                    ResourceManager.Instance.AddConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(farmCo2Change);
                    _currentCo2Change = farmCo2Change;
                    break;

                case BuildingType.Institute:
                    ResourceManager.Instance.AddConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(instituteCo2Change);
                    _currentCo2Change = instituteCo2Change;
                    break;

                case BuildingType.Bank:
                    ResourceManager.Instance.AddBank();
                    ResourceManager.Instance.AddConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.AddPowerPlantEffect(bankCo2Change);
                    _currentCo2Change = bankCo2Change;
                    break;

                case BuildingType.PowerPlant:
                    ResourceManager.Instance.AddPowerPlantEffect(powerPlantCo2Change);
                    _currentCo2Change = powerPlantCo2Change;
                    ResourceManager.Instance.AddGeneration(_currentPowerProduction);
                    break;

                case BuildingType.Co2Storage:
                    ResourceManager.Instance.AddCo2Absorption(storageCo2Change);
                    _currentCo2Change = -storageCo2Change;
                    _currentElectricityConsumption = storageConsumption;
                    ResourceManager.Instance.AddConsumption(_currentElectricityConsumption);
                    break;

                default:
                    ResourceManager.Instance.AddConsumption(_currentElectricityConsumption);
                    break;
            }
        }

        public void RemoveEffect()
        {
            if (!_isActive && ResourceManager.Instance == null) return;
            _isActive = false;

            switch (type)
            {
                case BuildingType.House:
                    ResourceManager.Instance.RemoveHouseEffect(populationCapacity, initialPopulation);
                    ResourceManager.Instance.RemoveConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(houseCo2Change);
                    break;

                case BuildingType.Farm:
                    ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
                    ResourceManager.Instance.RemoveConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(farmCo2Change);
                    break;

                case BuildingType.Institute:
                    ResourceManager.Instance.RemoveConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(instituteCo2Change);
                    break;

                case BuildingType.Bank:
                    ResourceManager.Instance.RemoveBank();
                    ResourceManager.Instance.RemoveConsumption(_currentElectricityConsumption);
                    ResourceManager.Instance.RemovePowerPlantEffect(bankCo2Change);
                    break;

                case BuildingType.PowerPlant:
                    ResourceManager.Instance.RemovePowerPlantEffect(powerPlantCo2Change);
                    ResourceManager.Instance.RemoveGeneration(_currentPowerProduction);
                    break;

                case BuildingType.Co2Storage:
                    ResourceManager.Instance.RemoveCo2Absorption(storageCo2Change);
                    ResourceManager.Instance.RemoveConsumption(_currentElectricityConsumption);
                    break;

                default:
                    ResourceManager.Instance.RemoveConsumption(_currentElectricityConsumption);
                    break;
            }
        }

        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;
            if (_currentHealth <= 0) DestroyBuilding();
        }

        public void Heal(float amount)
        {
            _currentHealth += amount;
            if (_currentHealth > maxHealth) _currentHealth = maxHealth;
        }

        public void UpdateElectricityConsumption(float newValue)
        {
            if (type == BuildingType.PowerPlant) return;
            if (_isActive) ResourceManager.Instance.RemoveConsumption(_currentElectricityConsumption);
            _currentElectricityConsumption = newValue;
            if (_isActive) ResourceManager.Instance.AddConsumption(_currentElectricityConsumption);
        }

        public void UpdateFoodProduction(float newValue)
        {
            if (type != BuildingType.Farm) return;
            if (_isActive) ResourceManager.Instance.RemoveFoodProduction(_currentFoodProduction);
            _currentFoodProduction = newValue;
            if (_isActive) ResourceManager.Instance.AddFoodProduction(_currentFoodProduction);
        }

        public float GetCurrentElectricity()
        {
            if (type == BuildingType.PowerPlant) return -_currentPowerProduction;
            return _currentElectricityConsumption;
        }

        public float GetCurrentFood() => _currentFoodProduction;
        public float GetCurrentCo2Change() => _currentCo2Change;

        public void DestroyBuilding()
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