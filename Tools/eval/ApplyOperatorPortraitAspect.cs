UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEngine.Canvas canvas = null;
foreach (UnityEngine.GameObject root in scene.GetRootGameObjects())
{
    if (root.name == "Canvas")
    {
        canvas = root.GetComponent<UnityEngine.Canvas>();
        break;
    }
}
if (canvas == null)
{
    throw new System.InvalidOperationException("TitleScene Canvas를 찾지 못했습니다.");
}

string[] paths =
{
    "MainMenuBackground/OperatorImage",
    "ShopPanelBackground/OperatorPanel/OperatorPortrait"
};
foreach (string path in paths)
{
    UnityEngine.Transform target = canvas.transform.Find(path);
    UnityEngine.UI.Image image = target != null
        ? target.GetComponent<UnityEngine.UI.Image>()
        : null;
    if (image == null)
    {
        throw new System.InvalidOperationException("Image를 찾지 못했습니다: " + path);
    }

    image.preserveAspect = true;
    UnityEditor.EditorUtility.SetDirty(image);
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
{
    throw new System.InvalidOperationException("TitleScene 저장에 실패했습니다.");
}

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
