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
        // --- 关键修改：我们现在查询的是 EventDirector 上的属性 ---
        if (director.currentGameDay < minGameDays) return false;
        if (maxGameDays > 0 && director.currentGameDay > maxGameDays) return false;

        // 检查我们项目中的真实变量
        if (director.playerPopulation < minPopulation) return false;
        if (director.happiness < minHappiness) return false;
        if (director.airQuality < minAirQuality) return false;

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