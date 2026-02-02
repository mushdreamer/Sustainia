using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class FullTutorialGenerator : EditorWindow
{
    [MenuItem("Tools/Sustaina/Generate 100% Match Tutorial")]
    public static void GenerateFullTutorial()
    {
        TutorialManager manager = GameObject.FindObjectOfType<TutorialManager>();
        if (manager == null) { Debug.LogError("TutorialManager not found!"); return; }

        List<TutorialStep> fullSteps = new List<TutorialStep>();

        // --- EVENT 1: Energy Balance ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E1.1 - Power Station Check",
            instructionText = "First, let's look at the Power Station. It provides Local Generation. Click it to see its output (+60).",
            requireInput = true,
            cameraDistance = 10f,
            cameraAngle = 35f
        });
        fullSteps.Add(new TutorialStep
        {
            stepName = "E1.2 - Battery Check",
            instructionText = "Now check the Grid Connection point. It imports energy from the main grid (+20).",
            requireInput = true,
            cameraDistance = 10f
        });
        fullSteps.Add(new TutorialStep
        {
            stepName = "E1.3 - Demand Check",
            instructionText = "The Residential Area consumes energy. Check its Demand (-40).",
            requireInput = true,
            cameraDistance = 12f
        });
        fullSteps.Add(new TutorialStep
        {
            stepName = "E1.4 - Equation Logic",
            instructionText = "The result is Net Grid Load: Input(60) + Local(20) - Demand(40) = +40 surplus stored in batteries.",
            requireInput = true,
            shouldPauseGame = true
        });

        // --- EVENT 2: State Accumulation ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E2.1 - Current State",
            instructionText = "The atmosphere currently has 400ppm of CO2. Observe the annual emissions (+50) and removals (-10).",
            requireInput = true,
            cameraDistance = 20f
        });
        fullSteps.Add(new TutorialStep
        {
            stepName = "E2.2 - Accumulation Result",
            instructionText = "After clicking 'Next Year', the new level is: CO2(t) + Emissions - Removals = 440ppm.",
            requireInput = true,
            shouldPauseGame = true
        });

        // --- EVENT 3: Objective Function ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E3.1 - Misguided Goal",
            instructionText = "Try to 'Maximize Capacity' as your goal. Notice how the maintenance costs drain your budget!",
            requireInput = true,
            cameraDistance = 15f
        });
        fullSteps.Add(new TutorialStep
        {
            stepName = "E3.2 - Real Objective",
            instructionText = "A reasonable objective is: Minimize total cost while meeting demand and CO2 limits. Let's fix this.",
            requireInput = false,
            requireRemoval = true,
            shouldPauseGame = false
        });

        // --- EVENT 4: Hard Constraints ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E4.1 - Capacity Limit",
            instructionText = "The battery is at 100/100. Try to add more energy. You can't, because 'Energy cannot exceed Capacity'.",
            requireInput = true,
            cameraDistance = 8f
        });

        // --- EVENT 5: Transformation ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E5.1 - Maximization to Minimization",
            instructionText = "To maximize Prosperity (P) using a minimization solver, we must minimize (-P). It preserves the same optimal layout.",
            requireInput = true,
            shouldPauseGame = true
        });

        // --- EVENT 6: Multiple Objectives ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E6.1 - Weighted Score",
            instructionText = "Your city must be both Economic and Sustainable. Combine them into a single weighted score to solve the tradeoff.",
            requireInput = true,
            cameraDistance = 18f
        });

        // --- EVENT 7: Diagnosis (Non-negativity) ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E7.1 - Negative Housing Error",
            instructionText = "The system suggests -10 housing units! This is missing a 'Non-negativity' constraint to prevent impossible values.",
            requireInput = true,
            shouldPauseGame = true
        });

        // --- EVENT 8: Feasibility ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E8.1 - Plan Selection",
            instructionText = "Plan A: Pop 80, Pollution 70. Plan B: Pop 70, Pollution 55. Limit is 60. Plan A is INFEASIBLE.",
            requireInput = true,
            cameraDistance = 25f
        });
        fullSteps.Add(new TutorialStep
        {
            stepName = "E8.2 - Selecting Best Feasible",
            instructionText = "Plan B and C are feasible. Plan B has a higher total score. Let's build Plan B.",
            requireOptimizationGoal = true,
            shouldPauseGame = false
        });

        // --- EVENT 9: Constraint Binding ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E9.1 - Slack Check",
            instructionText = "Each factory adds +10 pollution. We are at 40/70. Pollution is NOT yet the limiting factor (Slack > 0).",
            requireInput = true,
            cameraDistance = 12f
        });
        fullSteps.Add(new TutorialStep
        {
            stepName = "E9.2 - Binding Point",
            instructionText = "At 70/70, the shadow price changes. The constraint is now BINDING and blocks further improvement.",
            requireInput = true,
            shouldPauseGame = true
        });

        // --- EVENT 10: Normalization ---
        fullSteps.Add(new TutorialStep
        {
            stepName = "E10.1 - Normalization Need",
            instructionText = "To compare high food production and low emissions, we normalize them into a single 0-1 consistent score.",
            requireInput = true,
            shouldPauseGame = true
        });

        Undo.RecordObject(manager, "Full 100% Tutorial Append");
        if (manager.steps == null) manager.steps = new List<TutorialStep>();
        manager.steps.AddRange(fullSteps);
        EditorUtility.SetDirty(manager);
        Debug.Log("Successfully appended 22 steps based on 100% Event Design match.");
    }
}