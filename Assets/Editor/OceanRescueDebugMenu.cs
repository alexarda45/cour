using ChromaBlast;
using UnityEditor;
using UnityEngine;

public static class OceanRescueDebugMenu
{
    private const string MenuRoot = "Chroma Blast/Debug/Ocean Rescue/";
    private const string ShowPath = MenuRoot + "Show Ocean Rescue";
    private const string RewardPath = MenuRoot + "Simulate Reward Success";
    private const string FailurePath = MenuRoot + "Simulate Ad Failure";
    private const string ClosePath = MenuRoot + "Close Ocean Rescue";
    private const string ResetPath = MenuRoot + "Reset Ocean Rescue State";

    [MenuItem(ShowPath, false, 1)]
    private static void ShowOceanRescue()
    {
        Execute(controller => controller.DebugShowOceanRescue());
    }

    [MenuItem(RewardPath, false, 2)]
    private static void SimulateRewardSuccess()
    {
        Execute(controller => controller.DebugSimulateRewardSuccess());
    }

    [MenuItem(FailurePath, false, 3)]
    private static void SimulateAdFailure()
    {
        Execute(controller => controller.DebugSimulateAdFailure());
    }

    [MenuItem(ClosePath, false, 4)]
    private static void CloseOceanRescue()
    {
        Execute(controller => controller.DebugCloseOceanRescue());
    }

    [MenuItem(ResetPath, false, 5)]
    private static void ResetOceanRescueState()
    {
        Execute(controller => controller.DebugResetOceanRescueState());
    }

    [MenuItem(ShowPath, true)]
    private static bool ValidateShowOceanRescue()
    {
        return CanExecutePlayModeCommand();
    }

    [MenuItem(RewardPath, true)]
    private static bool ValidateSimulateRewardSuccess()
    {
        return CanExecutePlayModeCommand();
    }

    [MenuItem(FailurePath, true)]
    private static bool ValidateSimulateAdFailure()
    {
        return CanExecutePlayModeCommand();
    }

    [MenuItem(ClosePath, true)]
    private static bool ValidateCloseOceanRescue()
    {
        return CanExecutePlayModeCommand();
    }

    [MenuItem(ResetPath, true)]
    private static bool ValidateResetOceanRescueState()
    {
        return CanExecutePlayModeCommand();
    }

    private static bool CanExecutePlayModeCommand()
    {
        return EditorApplication.isPlaying && !EditorApplication.isCompiling;
    }

    private static void Execute(
        System.Func<OceanRescueController, bool> command)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "[Ocean Rescue Debug] This command is available only in Play Mode.");
            return;
        }

        OceanRescueController[] controllers =
            Object.FindObjectsByType<OceanRescueController>(
                FindObjectsInactive.Include);
        if (controllers.Length != 1)
        {
            Debug.LogWarning(
                $"[Ocean Rescue Debug] Expected exactly one OceanRescueController, found {controllers.Length}.");
            return;
        }

        command(controllers[0]);
    }
}
