using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

[CreateAssetMenu(fileName = "ResourceChangeAction", menuName = "Game Event System/Actions/Resource Change")]
public class ResourceChangeAction : GameEventAction
{
    [Header("Penalty Settings")]
    public float moneyChange = -500f;

    [Header("Feedback")]
    public string logMessage = "电力赤字导致电网受损，扣除维修费用！";

    public override void Execute()
    {
        if (ResourceManager.Instance != null)
        {
            // 执行扣费逻辑
            ResourceManager.Instance.AddMoney(moneyChange);
            Debug.Log($"<color=red>[事件执行]</color> {logMessage} 涉及金额: {moneyChange}");
        }
    }
}