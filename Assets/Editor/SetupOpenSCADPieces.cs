using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EditorTools
{
    public static class SetupOpenSCADPieces
    {
        private const string PREFAB_SETUP_VERSION_KEY = "SetupOpenSCADPieces_Version";
        private const string CURRENT_VERSION = "1.4_OpenSCAD_UpgradedColonySpaceport";

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            EditorApplication.delayCall += CheckAndRunSetup;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += CheckAndRunSetup;
            }
        }

        private static void CheckAndRunSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            string appliedVersion = EditorPrefs.GetString(PREFAB_SETUP_VERSION_KEY, "");
            if (appliedVersion != CURRENT_VERSION)
            {
                ExecuteSetup();
            }
        }

        [MenuItem("Tools/Switch to OpenSCAD Piece Prefabs")]
        [MenuItem("Deep Space Pioneers/Switch to OpenSCAD Piece Prefabs")]
        public static void ExecuteSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SetupOpenSCADPieces] Please exit Play Mode before running setup!");
                return;
            }

            Debug.Log("[SetupOpenSCADPieces] Starting setup of OpenSCAD pieces (Flush on grid, Redesigned Rocket Spaceport, No Hex Plate)...");

            // OpenSCAD meshes are exported with baked Unity coordinates (+Z forward, +Y up).
            Quaternion pieceRotation = Quaternion.identity;

            // Calibrated scale factors (~0.85 - 0.95 units)
            Vector3 colonyScale = new Vector3(0.036f, 0.036f, 0.036f);
            Vector3 spaceportScale = new Vector3(0.036f, 0.036f, 0.036f);
            Vector3 tradeBaseScale = new Vector3(0.036f, 0.036f, 0.036f);
            Vector3 shipScale = new Vector3(0.038f, 0.038f, 0.038f);

            // Ground height compensation: TriMap spawns colonies at Y=+0.1f.
            // Shifting child model down by -0.10f world units (-0.10f / scale.y) places the base flush on the grid plane (Y=0).
            Vector3 colonyGroundOffset = new Vector3(0f, -0.10f / colonyScale.y, 0f);
            Vector3 spaceportGroundOffset = new Vector3(0f, -0.10f / spaceportScale.y, 0f);
            Vector3 tradeBaseGroundOffset = new Vector3(0f, -0.12f / tradeBaseScale.y, 0f);
            Vector3 shipOffset = Vector3.zero;

            GameObject colonyPrefab = CreatePiecePrefab(
                "Assets/Models/Pieces/colony.obj",
                "Assets/Prefabs/ColonyPrefab.prefab",
                colonyScale,
                pieceRotation,
                colonyGroundOffset);

            GameObject spaceportPrefab = CreatePiecePrefab(
                "Assets/Models/Pieces/spaceport.obj",
                "Assets/Prefabs/SpaceportPrefab.prefab",
                spaceportScale,
                pieceRotation,
                spaceportGroundOffset);

            GameObject tradeBasePrefab = CreatePiecePrefab(
                "Assets/Models/Pieces/trade_station.obj",
                "Assets/Prefabs/TradeBasePrefab.prefab",
                tradeBaseScale,
                pieceRotation,
                tradeBaseGroundOffset);

            GameObject colonyShipPrefab = CreatePiecePrefab(
                "Assets/Models/Pieces/colony_ship.obj",
                "Assets/Prefabs/ColonyShipPrefab.prefab",
                shipScale,
                pieceRotation,
                shipOffset);

            GameObject tradeShipPrefab = CreatePiecePrefab(
                "Assets/Models/Pieces/trade_ship.obj",
                "Assets/Prefabs/TradeShipPrefab.prefab",
                shipScale,
                pieceRotation,
                shipOffset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Wire up in active scene or DeepSpacePioneers.unity
            string scenePath = "Assets/Scenes/DeepSpacePioneers.unity";
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != scenePath)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            TriMap triMap = Object.FindFirstObjectByType<TriMap>();
            if (triMap != null)
            {
                Undo.RecordObject(triMap, "Assign OpenSCAD Piece Prefabs");
                triMap.ColonyPrefab = colonyPrefab;
                triMap.SpaceportPrefab = spaceportPrefab;
                triMap.TradeBasePrefab = tradeBasePrefab;
                triMap.ColonyShipPrefab = colonyShipPrefab;
                triMap.TradeShipPrefab = tradeShipPrefab;
                PrefabUtility.RecordPrefabInstancePropertyModifications(triMap);
                EditorUtility.SetDirty(triMap);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[SetupOpenSCADPieces] Successfully assigned all 5 OpenSCAD prefabs to TriMap in DeepSpacePioneers scene!");
            }
            else
            {
                Debug.LogWarning("[SetupOpenSCADPieces] TriMap not found in active scene!");
            }

            EditorPrefs.SetString(PREFAB_SETUP_VERSION_KEY, CURRENT_VERSION);
            AssetDatabase.SaveAssets();
            Debug.Log("[SetupOpenSCADPieces] OpenSCAD pieces setup complete!");
        }

        private static GameObject CreatePiecePrefab(string modelPath, string prefabPath, Vector3 scale, Quaternion rotation, Vector3 localChildOffset)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
            {
                Debug.LogError("[SetupOpenSCADPieces] Could not load model: " + modelPath);
                return null;
            }

            MeshFilter modelMf = modelAsset.GetComponentInChildren<MeshFilter>();
            MeshRenderer modelMr = modelAsset.GetComponentInChildren<MeshRenderer>();

            if (modelMf == null || modelMf.sharedMesh == null)
            {
                Debug.LogError("[SetupOpenSCADPieces] Model has no MeshFilter or sharedMesh: " + modelPath);
                return null;
            }

            GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            root.transform.position = Vector3.zero;
            root.transform.rotation = rotation;
            root.transform.localScale = scale;

            GameObject modelChild = new GameObject("VisualModel");
            modelChild.transform.SetParent(root.transform, false);
            modelChild.transform.localPosition = localChildOffset;
            modelChild.transform.localRotation = Quaternion.identity;
            modelChild.transform.localScale = Vector3.one;

            MeshFilter mf = modelChild.AddComponent<MeshFilter>();
            mf.sharedMesh = modelMf.sharedMesh;

            MeshRenderer mr = modelChild.AddComponent<MeshRenderer>();
            if (modelMr != null && modelMr.sharedMaterials != null && modelMr.sharedMaterials.Length > 0 && modelMr.sharedMaterials[0] != null)
            {
                mr.sharedMaterials = modelMr.sharedMaterials;
            }
            else
            {
                Material mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                if (mat != null) mr.sharedMaterial = mat;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            Debug.Log("[SetupOpenSCADPieces] Created/Updated prefab: " + prefabPath);
            return savedPrefab;
        }
    }
}
