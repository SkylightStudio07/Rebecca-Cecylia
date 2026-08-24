using RCCom.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RCCom.Eval
{
    public static class BuildAuroraDialogueRunner
    {
        public static void Run()
        {
            AuroraDialogueBuilder.Build();
            OperatorBuildReport report = OperatorAssetBuilder.BuildSingle("Assets/Editor/OperatorRecipes/Aurora.json");
            Debug.Log($"[BuildAuroraDialogueRunner] Aurora Dialogue and Operator built successfully! Changed assets: {report.changedAssets.Count}, Validation: {report.validationPassed}");
        }
    }
}
