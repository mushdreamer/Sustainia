// EventDirector.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class EventDirector : MonoBehaviour
{
    public static EventDirector Instance;

    [Header("Event Pool")]
    [Tooltip("All Possible Event List")]
    public List<GameEvent> allEvents;

    [Header("When?")]
    [Tooltip("Min Day Between Event")]
    public float minTimeBetweenEvents = 20f;
    [Tooltip("Max Day Between Event")]
    public float maxTimeBetweenEvents = 60f;

    // --- 关键修改：从 ResourceManager 获取真实状态 ---
    // 我们不再使用自己的变量，而是创建属性来“转发”真实数据

    private ResourceManager _res; // 对 ResourceManager 的引用

    // --- 公开的游戏状态属性 (供 GameEvent 读取) ---
    public int currentGameDay => _res != null ? _res._currentDay : 0;
    public int playerPopulation => _res != null ? _res._currentPopulation : 0;
    public float money => _res != null ? _res.Money : 0;
    public float happiness => _res != null ? _res._happiness : 0;
    public float airQuality => _res != null ? _res._airQuality : 0;
    // ... 你可以在这里转发任何 GameEvent 需要的状态

    private float timer;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ResetTimer();
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            TryTriggerEvent();
            ResetTimer();
        }
    }

    private void ResetTimer()
    {
        timer = Random.Range(minTimeBetweenEvents, maxTimeBetweenEvents);
        Debug.Log($"Next Event will Come in {Mathf.RoundToInt(timer)} Days!");
    }

    public void TryTriggerEvent()
    {
        // 1. 筛选出所有当前条件满足的事件
        List<GameEvent> validEvents = allEvents.Where(e => e.AreConditionsMet(this)).ToList();

        if (validEvents.Count == 0)
        {
            Debug.Log("没有满足条件的事件可以触发。");
            return;
        }

        // 2. 根据权重计算总权重
        float totalWeight = validEvents.Sum(e => e.baseWeight);

        // 3. 进行加权随机选择
        float randomPoint = Random.Range(0, totalWeight);
        GameEvent chosenEvent = null;

        foreach (var e in validEvents)
        {
            if (randomPoint < e.baseWeight)
            {
                chosenEvent = e;
                break;
            }
            randomPoint -= e.baseWeight;
        }

        // 4. 执行选中的事件
        if (chosenEvent != null)
        {
            chosenEvent.Execute();
        }
    }
}