using UnityEngine;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

[CreateAssetMenu(fileName = "NewAddResourceAction", menuName = "Game Event System/Action/Add Resource", order = 50)]
public class AddResourceAction : GameEventAction
{
    public float moneyToAdd = 0;
    public float foodToAdd = 0;
    // ... 你可以添加更多 ...

    public override void Execute()
    {
        if (moneyToAdd != 0)
        {
            ResourceManager.Instance.AddMoney(moneyToAdd);
            Debug.Log($"[GameEvent] Add Money {moneyToAdd}!");
        }
        // 你需要为 ResourceManager 添加一个 public void AddFood(float amount) 的方法
        // if (foodToAdd != 0)
        // {
        //     ResourceManager.Instance.AddFood(foodToAdd);
        // }
    }
}