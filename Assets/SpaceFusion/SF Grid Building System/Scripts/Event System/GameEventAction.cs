// GameEventAction.cs
using UnityEngine;

// 这是一个抽象类，定义了所有事件动作的规范
public abstract class GameEventAction : ScriptableObject
{
    /// <summary>
    /// 执行此动作的具体逻辑
    /// </summary>
    public abstract void Execute();
}