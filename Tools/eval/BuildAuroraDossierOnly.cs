string recipePath = "Assets/Editor/OperatorRecipes/Aurora.json";
UnityEditor.TextAsset recipeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.TextAsset>(recipePath);
RCCom.EditorTools.OperatorAssetRecipe recipe = UnityEngine.JsonUtility.FromJson<RCCom.EditorTools.OperatorAssetRecipe>(recipeAsset.text);
RCCom.Definitions.Operator.OperatorDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>("Assets/Data/Operators/aurora/OperatorDefinition.asset");

definition.codename = recipe.codename ?? string.Empty;
definition.role = recipe.role ?? string.Empty;
definition.faction = recipe.faction ?? string.Empty;
definition.height = recipe.height ?? string.Empty;
definition.birthday = recipe.birthday ?? string.Empty;
definition.speciality = recipe.speciality ?? string.Empty;
definition.weapon = recipe.weapon ?? string.Empty;
definition.origin = recipe.origin ?? string.Empty;

definition.bondRecords = new System.Collections.Generic.List<RCCom.Definitions.Operator.OperatorBondRecord>();
if (recipe.bondRecords != null)
{
    foreach (var b in recipe.bondRecords)
    {
        definition.bondRecords.Add(new RCCom.Definitions.Operator.OperatorBondRecord
        {
            description = b != null ? b.description : string.Empty
        });
    }
}

UnityEditor.EditorUtility.SetDirty(definition);
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log("[BuildAuroraDossierOnly] Aurora Definition Dossier updated successfully!");
