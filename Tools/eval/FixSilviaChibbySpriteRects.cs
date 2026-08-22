string portraitFolder = "Assets/Art/Character Standing Arts/실비아/portrait";
string[] guids = UnityEditor.AssetDatabase.FindAssets("Chibby", new[] { portraitFolder });
System.Array.Sort(guids, (left, right) => string.CompareOrdinal(
    UnityEditor.AssetDatabase.GUIDToAssetPath(left), UnityEditor.AssetDatabase.GUIDToAssetPath(right)));
var paths = new System.Collections.Generic.List<string>();

for (int i = 0; i < guids.Length; i++)
{
    string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
    var importer = UnityEditor.AssetImporter.GetAtPath(assetPath);
    var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
    factory.Init();
    var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
    if (dataProvider == null)
    {
        throw new System.InvalidOperationException($"Sprite 데이터 제공자를 찾지 못했습니다: {assetPath}");
    }

    dataProvider.InitSpriteEditorDataProvider();
    var editCapability = dataProvider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
    if (editCapability == null)
    {
        throw new System.InvalidOperationException($"Sprite 편집 기능을 지원하지 않습니다: {assetPath}");
    }

    var capability = editCapability.GetEditCapability();
    if (!capability.HasCapability(UnityEditor.U2D.Sprites.EEditCapability.EditSpriteRect))
    {
        throw new System.InvalidOperationException($"Sprite 사각형 편집 기능을 지원하지 않습니다: {assetPath}");
    }

    UnityEngine.Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(assetPath);
    if (texture == null || texture.width <= 0 || texture.height <= 0)
    {
        throw new System.InvalidOperationException($"텍스처 크기를 읽지 못했습니다: {assetPath}");
    }

    UnityEditor.SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
    if (spriteRects == null || spriteRects.Length != 1)
    {
        throw new System.InvalidOperationException(
            $"Chibby 포트릿은 파일당 1개 슬라이스여야 합니다: {assetPath} ({(spriteRects == null ? 0 : spriteRects.Length)}개)");
    }

    paths.Add(assetPath);
}

var missingSprites = new System.Collections.Generic.List<string>();
for (int i = 0; i < paths.Count; i++)
{
    string assetPath = paths[i];
    var importer = UnityEditor.AssetImporter.GetAtPath(assetPath);
    var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
    factory.Init();
    var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
    dataProvider.InitSpriteEditorDataProvider();
    UnityEditor.SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
    UnityEngine.Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(assetPath);
    spriteRects[0].rect = new UnityEngine.Rect(0f, 0f, texture.width, texture.height);
    dataProvider.SetSpriteRects(spriteRects);
    dataProvider.Apply();

    // Unity 6의 provider가 새로 기록하는 루트 ID는 이 프로젝트의 기존 단일 Sprite
    // 에셋에는 없고, 불필요한 importer 차이만 만든다. 슬라이스 ID는 유지한 채 루트 값만
    // 에디터 직렬화 API로 비워 기존 에셋과 같은 구조로 저장한다.
    var serializedImporter = new UnityEditor.SerializedObject(importer);
    var rootSpriteId = serializedImporter.FindProperty("m_SpriteSheet.m_SpriteID");
    if (rootSpriteId == null || rootSpriteId.propertyType != UnityEditor.SerializedPropertyType.String)
    {
        throw new System.InvalidOperationException($"Sprite 루트 ID 필드를 찾지 못했습니다: {assetPath}");
    }

    rootSpriteId.stringValue = string.Empty;
    serializedImporter.ApplyModifiedPropertiesWithoutUndo();
    importer.SaveAndReimport();

    UnityEngine.Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(assetPath);
    if (sprite == null)
    {
        missingSprites.Add(assetPath);
    }

    UnityEngine.Debug.Log(
        $"[FixSilviaChibbySpriteRects] {assetPath}: rect={spriteRects[0].rect}, " +
        $"sprite={(sprite == null ? "<null>" : sprite.name)}");
}

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
if (missingSprites.Count > 0)
{
    throw new System.InvalidOperationException(
        $"Sprite 서브에셋 생성에 실패한 파일 {missingSprites.Count}개: {string.Join(", ", missingSprites)}");
}

UnityEngine.Debug.Log($"[FixSilviaChibbySpriteRects] {paths.Count}개 Chibby 포트릿 rect 복구 및 Sprite 생성 완료");
