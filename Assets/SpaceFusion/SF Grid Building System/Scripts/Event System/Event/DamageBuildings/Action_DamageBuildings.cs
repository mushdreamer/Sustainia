// Action_DamageBuildings.cs
using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

[CreateAssetMenu(fileName = "NewDamageBuildingsAction", menuName = "Game Event System/Action/Damage Buildings", order = 101)]
public class Action_DamageBuildings : GameEventAction
{
    [Tooltip("对所有建筑造成多少点伤害")]
    public float damageAmount = 25f;

    // 您未来可以扩展这里，比如只伤害特定类型的建筑
    // public BuildingType targetType; 

    public override void Execute()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("Action_DamageBuildings: ResourceManager.Instance is null!");
            return;
        }

        Debug.Log($"Event causing {damageAmount} damage to all buildings.");

        // 1. 从 ResourceManager 获取所有已放置建筑的列表
        var allBuildings = ResourceManager.Instance.GetAllPlacedBuildings();

        // 2. 遍历列表并对每个建筑造成伤害
        foreach (var building in allBuildings)
        {
            if (building != null) // 确保建筑在循环中没有被摧毁
            {
                building.TakeDamage(damageAmount);
            }
        }
    }
}