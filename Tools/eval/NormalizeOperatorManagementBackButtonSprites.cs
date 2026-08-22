const string path = "Assets/Art/UI/OperatorManaging/OperatorManagementBackButtonSheet.png";

UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
if (importer == null)
{
    throw new System.InvalidOperationException("Back 버튼 스프라이트 시트를 찾지 못했습니다.");
}

var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
factories.Init();
UnityEditor.U2D.Sprites.ISpriteEditorDataProvider provider =
    factories.GetSpriteEditorDataProviderFromObject(importer);
provider.InitSpriteEditorDataProvider();

UnityEditor.SpriteRect[] rects = provider.GetSpriteRects();
bool changed = false;
for (int i = 0; i < rects.Length; i++)
{
    if (rects[i].name == "OperatorManagementBackButtonSheet_7")
    {
        rects[i].name = "OperatorManagementBackButtonSheet_1";
        changed = true;
    }
}

if (changed)
{
    provider.SetSpriteRects(rects);
    provider.Apply();
    importer.SaveAndReimport();
}

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log(changed
    ? "[OperatorManagement] Back 버튼 Hover 슬라이스를 _1로 정규화했습니다."
    : "[OperatorManagement] Back 버튼 슬라이스 이름이 이미 정규화되어 있습니다.");
