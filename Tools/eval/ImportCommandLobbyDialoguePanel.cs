UnityEditor.AssetDatabase.Refresh();

const string AssetPath = "Assets/Art/UI/Sprites/CommandLobbyDialoguePanel-v1.png";
UnityEditor.TextureImporter importer =
    UnityEditor.AssetImporter.GetAtPath(AssetPath) as UnityEditor.TextureImporter;

if (importer == null)
{
    throw new System.InvalidOperationException("로비 대사 패널 TextureImporter를 찾지 못했습니다.");
}

importer.textureType = UnityEditor.TextureImporterType.Sprite;
importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
importer.alphaIsTransparency = true;
importer.mipmapEnabled = false;
importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
importer.maxTextureSize = 2048;
importer.SaveAndReimport();

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log("[CommandLobbyDialoguePanel] UI Sprite 임포트 설정 완료");
