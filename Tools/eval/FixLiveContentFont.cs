// MessageText가 기본 LiberationSans SDF를 써서 한글이 두부(□)로 깨지는 문제를 수정한다.
// 프로젝트의 다른 한글 TMP 텍스트(DialogueText 등)와 동일한 Pretendard-Bold SDF를 사용한다.
UnityEngine.GameObject textGo = UnityEngine.GameObject.Find("Canvas/LiveContentLoadingPanel/MessageText");
if (textGo == null)
{
    UnityEngine.Debug.LogError("[FixLiveContentFont] MessageText를 찾지 못했습니다.");
}
else
{
    var tmp = textGo.GetComponent<TMPro.TextMeshProUGUI>();
    var fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
        "Assets/Resource/Font/Pretendard-Bold SDF.asset");
    if (fontAsset == null)
    {
        UnityEngine.Debug.LogError("[FixLiveContentFont] Pretendard-Bold SDF 폰트 에셋을 찾지 못했습니다.");
    }
    else
    {
        tmp.font = fontAsset;
        UnityEditor.EditorUtility.SetDirty(tmp);

        UnityEngine.SceneManagement.Scene activeScene =
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
        UnityEngine.Debug.Log("[FixLiveContentFont] MessageText 폰트를 Pretendard-Bold SDF로 교체 완료");
    }
}
