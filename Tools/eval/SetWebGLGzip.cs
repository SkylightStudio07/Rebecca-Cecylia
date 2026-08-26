UnityEditor.PlayerSettings.WebGL.compressionFormat = UnityEditor.WebGLCompressionFormat.Gzip;
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log(
    "[WebGLSettings] compression=" + UnityEditor.PlayerSettings.WebGL.compressionFormat +
    ", decompressionFallback=" + UnityEditor.PlayerSettings.WebGL.decompressionFallback);
