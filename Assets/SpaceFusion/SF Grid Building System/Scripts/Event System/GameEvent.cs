// GameEvent.cs
using UnityEngine;

// 这个特性让我们可以直接在Unity编辑器里右键创建事件资产
[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Game Event System/Game Event", order = 1)]
public class GameEvent : ScriptableObject
{
    [Header("Basic Info")]
    public string eventName;
    [TextArea(3, 5)]
    public string description;

    [Header("Event Condition")]
    [Tooltip("Min Day Between Event(0 for no limitation)")]
    public int minGameDays = 0;
    [Tooltip("Max Day Between Event(0 for no limitation)")]
    public int maxGameDays = 0;

    // --- 在这里添加你需要的真实条件 ---
    [Tooltip("Min Population for Event to Happen")]
    public int minPopulation = 4;
    [Tooltip("Min Happiness for Event to Happen")]
    public float minHappiness = 100f;
    [Tooltip("Min Air Quality for Event to Happen")]
    public float minAirQuality = 100f;

    [Header("Energy Conditions (New)")]
    [Tooltip("如果勾选，将检查电力平衡是否低于下方阈值")]
    public bool checkEnergyBalance = false;
    [Tooltip("触发事件所需的最高电力差额 (例如 -1 表示只有赤字严重时触发)")]
    public float maxAllowedEnergyBalance = 0f;

    [Header("Weight")]
    [Tooltip("The More Weight You Add on Your Event, More Easily to Happen")]
    public float baseWeight = 10f;

    [Header("Event Effect")]
    [Tooltip("Event Actions")]
    public GameEventAction[] actions;

    /// <summary>
    /// 检查当前游戏状态是否满足此事件的触发条件
    /// </summary>
    /// <param name="director">事件导演，用于获取当前游戏状态</param>
    /// <returns>如果满足条件则返回true</returns>
    public bool AreConditionsMet(EventDirector director)
    {
        // 检查游戏天数
        if (director.currentGameDay < minGameDays) return false;
        if (maxGameDays > 0 && director.currentGameDay > maxGameDays) return false;

        // 检查我们项目中的真实变量
        if (director.playerPopulation < minPopulation) return false;
        if (director.happiness < minHappiness) return false;
        if (director.airQuality < minAirQuality) return false;

        // --- 新增：检查电力差额 ---
        if (checkEnergyBalance)
        {
            // 如果当前的电力盈余 > 设定的阈值 (比如当前是 +10，阈值是 -1)，则不触发
            // 我们只在 电力平衡 < 阈值 时触发 (比如当前是 -5，阈值是 -1，触发)
            if (director.electricityBalance >= maxAllowedEnergyBalance) return false;
        }

        return true;
    }

    /// <summary>
    /// 执行事件
    /// </summary>
    public void Execute()
    {
        Debug.Log($"Event Happen: [{eventName}] - {description}");
        foreach (var action in actions)
        {
            action.Execute();
        }
    }
}