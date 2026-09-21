using UnityEditor;
using UnityEditor.SceneManagement;

namespace WanRen.Editor
{
    public static class NeedlePileMenu
    {
        const string ScenePath = "Assets/WanRen/Scenes/NeedlePile.unity";

        [MenuItem("WanRen/打开针堆场景")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
