UnityEditor.AssetDatabase.Refresh();
RCCom.EditorTools.AuroraDialogueBuilder.Build();
RCCom.EditorTools.OperatorAssetBuilder.BuildSingle("Assets/Editor/OperatorRecipes/Aurora.json");
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
