if (UnityEditor.EditorApplication.isPlaying)
{
    throw new System.InvalidOperationException("이미 Play Mode입니다.");
}

UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity");
UnityEditor.EditorApplication.isPlaying = true;
