using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TutorialManager))]
public class TutorialManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认的 Inspector 内容
        DrawDefaultInspector();

        TutorialManager manager = (TutorialManager)target;

        GUILayout.Space(10);
        GUI.enabled = Application.isPlaying; // 仅在运行模式下可以点击

        if (GUILayout.Button("Skip Current Step", GUILayout.Height(30)))
        {
            manager.SkipCurrentStep();
        }

        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the Skip button.", MessageType.Info);
        }
    }
}