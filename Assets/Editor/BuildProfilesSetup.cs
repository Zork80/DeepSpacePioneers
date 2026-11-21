using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EditorTools
{
    [InitializeOnLoad]
    public static class BuildProfilesSetup
    {
        private const string ScenePath = "Assets/Scenes/DeepSpacePioneers.unity";

        public static readonly string BuildsRootDir = "Builds";
        public static readonly string LinuxClientPath = "Builds/Linux_Client/DeepSpacePioneers.x86_64";
        public static readonly string LinuxServerPath = "Builds/Linux_DedicatedServer/DeepSpacePioneers_Server.x86_64";
        public static readonly string WindowsClientPath = "Builds/Windows_Client/DeepSpacePioneers.exe";

        private static StandaloneBuildSubtarget _lastSubtarget;
        private static UnityEditor.BuildTarget _lastTarget;

        static BuildProfilesSetup()
        {
            EnsureDirectories();
            _lastSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            _lastTarget = EditorUserBuildSettings.activeBuildTarget;
            SyncAllBuildLocations();

            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            var currentTarget = EditorUserBuildSettings.activeBuildTarget;
            var currentSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            if (currentTarget != _lastTarget || currentSubtarget != _lastSubtarget)
            {
                _lastTarget = currentTarget;
                _lastSubtarget = currentSubtarget;
                SyncAllBuildLocations();
            }
        }

        private static void EnsureDirectories()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Directory.CreateDirectory(Path.Combine(root, "Builds", "Linux_Client"));
            Directory.CreateDirectory(Path.Combine(root, "Builds", "Linux_DedicatedServer"));
            Directory.CreateDirectory(Path.Combine(root, "Builds", "Windows_Client"));
        }

        public static string GetSuggestedPath(UnityEditor.BuildTarget target, StandaloneBuildSubtarget subtarget)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (target == UnityEditor.BuildTarget.StandaloneLinux64)
            {
                if (subtarget == StandaloneBuildSubtarget.Server)
                {
                    return Path.Combine(root, LinuxServerPath);
                }
                return Path.Combine(root, LinuxClientPath);
            }
            if (target == UnityEditor.BuildTarget.StandaloneWindows64)
            {
                // For Windows builds on Linux, Unity prompts for an output directory rather than a file.
                // Pointing to 'Builds/Windows_Client' pre-fills 'Windows_Client' as the folder name.
                return Path.Combine(root, "Builds", "Windows_Client");
            }
            return Path.Combine(root, "Builds", "Default", "DeepSpacePioneers");
        }

        public static void SyncAllBuildLocations()
        {
            EnsureDirectories();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            StandaloneBuildSubtarget subtarget = EditorUserBuildSettings.standaloneBuildSubtarget;

            // Linux build location (File path for executable)
            string linuxPath = subtarget == StandaloneBuildSubtarget.Server ? LinuxServerPath : LinuxClientPath;
            EditorUserBuildSettings.SetBuildLocation(UnityEditor.BuildTarget.StandaloneLinux64, Path.Combine(root, linuxPath));

            // Windows build location (Folder path for Unity's SaveFolderPanel on Linux)
            EditorUserBuildSettings.SetBuildLocation(UnityEditor.BuildTarget.StandaloneWindows64, Path.Combine(root, "Builds", "Windows_Client"));
        }

        public static void SyncBuildLocation(UnityEditor.BuildTarget target, StandaloneBuildSubtarget subtarget)
        {
            SyncAllBuildLocations();
        }

        private static BuildPlayerOptions OnGetBuildPlayerOptions(BuildPlayerOptions defaultOptions)
        {
            EnsureDirectories();
            if (defaultOptions.target == UnityEditor.BuildTarget.NoTarget)
            {
                defaultOptions.target = EditorUserBuildSettings.activeBuildTarget;
            }

            StandaloneBuildSubtarget subtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            string suggestedPath = GetSuggestedPath(defaultOptions.target, subtarget);

            defaultOptions.locationPathName = suggestedPath;
            defaultOptions.subtarget = (int)subtarget;
            if (defaultOptions.scenes == null || defaultOptions.scenes.Length == 0)
            {
                defaultOptions.scenes = new string[] { ScenePath };
            }

            EditorUserBuildSettings.SetBuildLocation(defaultOptions.target, suggestedPath);
            Debug.Log($"[BuildProfilesSetup] Pre-populating build target={defaultOptions.target}, subtarget={subtarget}, path={suggestedPath}");
            return defaultOptions;
        }

        private static void OnBuildPlayer(BuildPlayerOptions options)
        {
            if (options.target == UnityEditor.BuildTarget.NoTarget)
            {
                options.target = EditorUserBuildSettings.activeBuildTarget;
            }

            StandaloneBuildSubtarget subtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            options.subtarget = (int)subtarget;

            if (string.IsNullOrEmpty(options.locationPathName) || options.locationPathName.Contains("Builds/Default"))
            {
                options.locationPathName = GetSuggestedPath(options.target, subtarget);
            }

            if (options.scenes == null || options.scenes.Length == 0)
            {
                options.scenes = new string[] { ScenePath };
            }

            string fullPath = Path.GetFullPath(options.locationPathName);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Debug.Log($"[BuildProfilesSetup] Building '{options.locationPathName}' ({options.target}, subtarget: {options.subtarget})...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildProfilesSetup] Build succeeded: {options.locationPathName} ({summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:F1}s).");
            }
            else
            {
                Debug.LogError($"[BuildProfilesSetup] Build failed with result: {summary.result}");
            }
        }

        [MenuItem("Build/Upgrade EventSystem in Scene", false, 40)]
        public static void UpgradeEventSystemInScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es != null)
            {
                var standalone = es.GetComponent<StandaloneInputModule>();
                if (standalone != null)
                {
                    UnityEngine.Object.DestroyImmediate(standalone, true);
                }

                var inputModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (inputModule == null)
                {
                    inputModule = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }

                string defaultAssetPath = "Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/PlayerInput/DefaultInputActions.inputactions";
                var defaultAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(defaultAssetPath);
                if (defaultAsset != null)
                {
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(defaultAssetPath)
                        .OfType<UnityEngine.InputSystem.InputActionReference>()
                        .ToArray();

                    inputModule.actionsAsset = defaultAsset;
                    inputModule.point = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "Point" || a.action.name == "MousePosition"));
                    inputModule.leftClick = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "Click" || a.action.name == "LeftClick"));
                    inputModule.rightClick = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "RightClick" || a.action.name == "Right Click"));
                    inputModule.middleClick = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "MiddleClick" || a.action.name == "Middle Click"));
                    inputModule.scrollWheel = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "ScrollWheel" || a.action.name == "Scroll Wheel"));
                    inputModule.move = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "Navigate" || a.action.name == "Move"));
                    inputModule.submit = subAssets.FirstOrDefault(a => a.action != null && a.action.name == "Submit");
                    inputModule.cancel = subAssets.FirstOrDefault(a => a.action != null && a.action.name == "Cancel");
                    inputModule.trackedDevicePosition = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "TrackedDevicePosition" || a.action.name == "Position"));
                    inputModule.trackedDeviceOrientation = subAssets.FirstOrDefault(a => a.action != null && (a.action.name == "TrackedDeviceOrientation" || a.action.name == "Orientation"));

                    Debug.Log($"[BuildProfilesSetup] Successfully configured InputSystemUIInputModule with {subAssets.Length} action references from {defaultAssetPath}!");
                }
                else
                {
                    Debug.LogWarning($"[BuildProfilesSetup] Could not load default input actions from {defaultAssetPath}. Falling back to AssignDefaultActions().");
                    inputModule.AssignDefaultActions();
                }

                var adapter = es.GetComponent<InputEventSystemAdapter>();
                if (adapter == null)
                {
                    es.gameObject.AddComponent<InputEventSystemAdapter>();
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                Debug.Log("[BuildProfilesSetup] Saved scene with upgraded EventSystem!");
            }
        }

        [MenuItem("Build/1. Build Linux Client (to Builds/Linux_Client/)", false, 10)]
        public static bool BuildLinuxClient()
        {
            return PerformBuild(UnityEditor.BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Player, LinuxClientPath, "Linux Client");
        }

        [MenuItem("Build/2. Build Linux Dedicated Server (to Builds/Linux_DedicatedServer/)", false, 11)]
        public static void BuildLinuxServer()
        {
            PerformBuild(UnityEditor.BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, LinuxServerPath, "Linux Dedicated Server");
        }

        [MenuItem("Build/3. Build Windows Client (to Builds/Windows_Client/)", false, 12)]
        public static void BuildWindowsClient()
        {
            PerformBuild(UnityEditor.BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, WindowsClientPath, "Windows Client");
        }

        [MenuItem("Build/Clean Root Directory Artifacts", false, 30)]
        public static void CleanRootArtifacts()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string[] filesToRemove = new string[]
            {
                "DeepSpacePioneers.x86_64",
                "UnityPlayer.so",
                "libdecor-0.so.0",
                "libdecor-cairo.so"
            };

            string[] dirsToRemove = new string[]
            {
                "DeepSpacePioneers_Data",
                "Deep Space Pioneers_BurstDebugInformation_DoNotShip"
            };

            int removedFiles = 0;
            foreach (string file in filesToRemove)
            {
                string path = Path.Combine(projectRoot, file);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    removedFiles++;
                }
            }

            int removedDirs = 0;
            foreach (string dir in dirsToRemove)
            {
                string path = Path.Combine(projectRoot, dir);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    removedDirs++;
                }
            }

            Debug.Log($"[BuildProfilesSetup] Cleaned root directory: removed {removedFiles} file(s) and {removedDirs} folder(s).");
        }

        private static bool PerformBuild(UnityEditor.BuildTarget target, StandaloneBuildSubtarget subtarget, string outputPath, string displayName)
        {
            string fullPath = Path.GetFullPath(outputPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorUserBuildSettings.standaloneBuildSubtarget = subtarget;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new string[] { ScenePath },
                locationPathName = outputPath,
                target = target,
                subtarget = (int)subtarget,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildProfilesSetup] Starting build for {displayName} -> {outputPath}...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildProfilesSetup] Build succeeded for {displayName}! Total size: {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:F1}s.");
                return true;
            }
            else
            {
                Debug.LogError($"[BuildProfilesSetup] Build failed for {displayName} with result: {summary.result}");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Build failed: {summary.result}, errors: {summary.totalErrors}");
                if (report.steps != null)
                {
                    foreach (var step in report.steps)
                    {
                        if (step.messages != null)
                        {
                            foreach (var msg in step.messages)
                            {
                                if (msg.type == LogType.Error || msg.type == LogType.Exception || msg.type == LogType.Assert)
                                {
                                    sb.AppendLine($"[{msg.type}] {msg.content}");
                                }
                            }
                        }
                    }
                }
                string errPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "build_error.txt");
                File.WriteAllText(errPath, sb.ToString());
                return false;
            }
        }
    }

    [InitializeOnLoad]
    public static class BuildTriggerWatcher
    {
        private static bool _isBuilding = false;

        static BuildTriggerWatcher()
        {
            EditorApplication.update += CheckTrigger;
        }

        private static void CheckTrigger()
        {
            if (_isBuilding) return;

            string triggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_build.txt");
            if (File.Exists(triggerPath))
            {
                _isBuilding = true;
                try
                {
                    string triggerText = File.ReadAllText(triggerPath);
                    File.Delete(triggerPath);
                    string donePath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "build_done.txt");
                    if (File.Exists(donePath)) File.Delete(donePath);

                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    if (triggerText.Contains("setup_pieces"))
                    {
                        SetupOpenSCADPieces.ExecuteSetup();
                    }
                    BuildProfilesSetup.UpgradeEventSystemInScene();
                    Debug.Log("[BuildTriggerWatcher] Trigger file detected! Initiating BuildLinuxClient()...");
                    bool ok = BuildProfilesSetup.BuildLinuxClient();
                    if (ok)
                    {
                        File.WriteAllText(donePath, "SUCCESS\n" + System.DateTime.UtcNow.Ticks);
                    }
                    else
                    {
                        string errPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "build_error.txt");
                        string details = File.Exists(errPath) ? File.ReadAllText(errPath) : "Build failed without details";
                        File.WriteAllText(donePath, "ERROR: " + details);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[BuildTriggerWatcher] Build error: {ex.Message}");
                    string donePath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "build_done.txt");
                    File.WriteAllText(donePath, "ERROR: " + ex.Message);
                }
                finally
                {
                    _isBuilding = false;
                }
            }
        }
    }
}

