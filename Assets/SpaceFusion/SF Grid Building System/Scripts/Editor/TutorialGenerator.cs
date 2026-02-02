using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TutorialGenerator : EditorWindow
{
    [MenuItem("Tools/Sustaina/Append English Optimization Tutorial")]
    public static void AppendTutorial()
    {
        List<TutorialStep> newSteps = new List<TutorialStep>();

        // Step 1: Energy Balance (QA 1)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 1: Energy Balance",
            instructionText = "Observe the energy panel. Formula: Grid Input + Local Generation - Demand = Storage Change[cite: 5, 50]. Currently: 60 + 20 - 40 = +40[cite: 5]. Surplus energy is now stored in the battery[cite: 6, 52].",
            requireInput = true,
            shouldPauseGame = true
        });

        // Step 2: State Accumulation (QA 2)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 2: Accumulation",
            instructionText = "CO2 levels are cumulative. Next Year = Current Level + (Emissions - Removals)[cite: 11, 57]. Click 'Next' to see the counter roll from 400 up to 440[cite: 11].",
            requireInput = true,
            shouldPauseGame = true
        });

        // Step 3: Objective Function (QA 3)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 3: Objective",
            instructionText = "Alert: You tried to maximize capacity, causing a massive deficit[cite: 13, 14]! Remember, the goal is to 'Minimize Cost while meeting demand'[cite: 16, 63]. Enter Demolish Mode to remove excess buildings.",
            requireInput = false,
            requireRemoval = true,
            shouldPauseGame = false
        });

        // Step 4: Hard Constraints (QA 4)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 4: Hard Constraint",
            instructionText = "Force Charge failed! The battery is full (100/100)[cite: 17]. This is a 'Hard Constraint': Storage Level cannot exceed its Capacity[cite: 20, 21, 68].",
            requireInput = true,
            shouldPauseGame = true
        });

        // Step 5: Transformation (QA 5)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 5: Transformation",
            instructionText = "The software only has a 'MINIMIZE' button[cite: 23]. To Maximize Prosperity (P), we must input '-P'[cite: 77]. Minimizing a negative value is mathematically equivalent to maximizing the positive value[cite: 24, 25].",
            requireInput = true,
            shouldPauseGame = true
        });

        // Step 6: Weighted Sum (QA 6)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 6: Weighted Score",
            instructionText = "How to balance High Profit and Low Pollution[cite: 26]? We combine objectives into a single Weighted Score[cite: 30, 84]. Score = w1*Gold + w2*Green[cite: 30].",
            requireInput = true,
            shouldPauseGame = true
        });

        // Step 7: Non-negativity (QA 7)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 7: Non-negativity",
            instructionText = "Model Crash! The system suggested building -5 houses[cite: 31, 33]. In reality, physical objects must follow the Non-Negativity Constraint (x ≥ 0)[cite: 34, 90].",
            requireInput = true,
            shouldPauseGame = true
        });

        // Step 8: Feasibility (QA 8)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 8: Feasibility",
            instructionText = "Plan A has higher profit but exceeds pollution limits (70/60), making it an Infeasible Solution[cite: 37, 39, 95]. Select Plan B, which is the highest score within limits[cite: 40, 101].",
            requireInput = false,
            requireOptimizationGoal = true,
            shouldPauseGame = false
        });

        // Step 9: Binding Constraints (QA 9)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 9: Binding",
            instructionText = "When pollution hits 70/70, Slack equals 0[cite: 42, 44]. The constraint is now 'Binding' and becomes the bottleneck preventing further expansion[cite: 44, 108].",
            requireInput = true,
            shouldPauseGame = true
        });

        // Step 10: Normalization (QA 10)
        newSteps.Add(new TutorialStep
        {
            stepName = "Event 10: Normalization",
            instructionText = "To compare tons of food vs. carbon emissions, we use 'Normalization'[cite: 48, 115]. This converts different units into a consistent 0-1 scale for a fair Star Rating[cite: 47, 48].",
            requireInput = true,
            shouldPauseGame = true
        });

        TutorialManager manager = GameObject.FindObjectOfType<TutorialManager>();
        if (manager != null)
        {
            Undo.RecordObject(manager, "Append Tutorial Steps");

            // 初始化列表以防为 null
            if (manager.steps == null) manager.steps = new List<TutorialStep>();

            // 使用 AddRange 实现追加而非覆盖
            manager.steps.AddRange(newSteps);

            EditorUtility.SetDirty(manager);
            Debug.Log($"Successfully appended {newSteps.Count} English optimization steps to your existing tutorial list [cite: 1-117].");
        }
        else
        {
            Debug.LogError("TutorialManager not found in the scene!");
        }
    }
}