using UnityEngine;
using UnityEngine.UI;
using SpaceFusion.SF_Grid_Building_System.Scripts.Managers;

public class IntroManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("全屏的介绍面板，包含背景图和文字")]
    public GameObject introPanel;

    [Tooltip("开始游戏的按钮")]
    public Button startButton;

    [Header("Settings")]
    [Tooltip("介绍面板是否一开始就显示？")]
    public bool showOnAwake = true;

    private void Awake()
    {
        // 1. 绑定按钮事件
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }

        // 2. 初始化状态
        if (showOnAwake)
        {
            ShowIntro();
        }
        else
        {
            // 如果不显示介绍，直接开始游戏
            StartGameLogic();
        }
    }

    private void OnDestroy()
    {
        //这是个好习惯：销毁时移除监听，防止内存泄漏
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }
    }

    private void ShowIntro()
    {
        // 激活全屏 UI
        if (introPanel != null) introPanel.SetActive(true);

        // --- 核心逻辑：暂停时间 ---
        // 将时间流逝设为 0，这会暂停所有基于 Time.deltaTime 的逻辑
        // 包括：ResourceManager 的资源计算、EventDirector 的倒计时、以及生成器的动画
        Time.timeScale = 0f;
    }

    private void OnStartClicked()
    {
        // 隐藏 UI
        if (introPanel != null) introPanel.SetActive(false);

        // --- 核心逻辑：恢复时间 ---
        Time.timeScale = 1f;

        // 触发游戏逻辑
        StartGameLogic();
    }

    private void StartGameLogic()
    {
        Debug.Log("[IntroManager] 游戏正式开始！");

        // 1. 通知生成器开始生成城市
        if (MultiZoneCityGenerator.Instance != null)
        {
            MultiZoneCityGenerator.Instance.BeginGeneration();
        }

        // 2. 这里可以添加其他游戏开始时的逻辑，比如播放背景音乐等
    }
}