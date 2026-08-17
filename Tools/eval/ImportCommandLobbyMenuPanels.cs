UnityEditor.AssetDatabase.Refresh();

string[] assetPaths =
{
    "Assets/Art/UI/Sprites/CommandLobbyMenu/LiveContent-Normal-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/LiveContent-Hover-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Operators-Normal-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Operators-Hover-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Operation-Normal-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Operation-Hover-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Records-Normal-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Records-Hover-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Configuration-Normal-v1.png",
    "Assets/Art/UI/Sprites/CommandLobbyMenu/Configuration-Hover-v1.png",
};

foreach (string assetPath in assetPaths)
{
    UnityEditor.TextureImporter importer =
        UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.TextureImporter;

    if (importer == null)
    {
        throw new System.InvalidOperationException($"메뉴 패널 TextureImporter를 찾지 못했습니다: {assetPath}");
    }

    importer.textureType = UnityEditor.TextureImporterType.Sprite;
    importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
    importer.alphaIsTransparency = true;
    importer.mipmapEnabled = false;
    importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
    importer.maxTextureSize = 2048;
    importer.SaveAndReimport();
}

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log("[CommandLobbyMenu] 메뉴 패널 10종 UI Sprite 임포트 설정 완료");
