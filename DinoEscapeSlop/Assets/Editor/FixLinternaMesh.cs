using UnityEditor;
using UnityEngine;

public static class FixLinternaMesh
{
    [MenuItem("Tools/Fix Linterna Mesh References")]
    public static void FixMeshReferences()
    {
        string modelPath = "Assets/_3DStealthGame/Tutorial_Demo/Demo_Prefabs/Key&Doors/linterna-cuarto/source/Linterna.obj";
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);

        Mesh foundMesh = null;
        foreach (Object sub in subAssets)
        {
            if (sub is Mesh m && foundMesh == null)
            {
                foundMesh = m;
                break;
            }
        }

        if (foundMesh != null)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(foundMesh, out string meshGuid, out long meshFileId);

            string prefabPath = "Assets/Prefabs/Interactables/Linterna.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot != null)
            {
                MeshFilter mf = prefabRoot.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    mf.sharedMesh = foundMesh;
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    Debug.Log($"[FixLinternaMesh] Linterna.prefab actualizado con la malla: {foundMesh.name} (FileID: {meshFileId})");
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
