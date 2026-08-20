#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShieldGame.Editor
{
    public static class ShieldProjectValidator
    {
        private const string HomeScenePath = "Assets/SHIELD/Scenes/Home.unity";
        private const string GameScenePath = "Assets/SHIELD/Scenes/Game.unity";

        [MenuItem("Tools/SHIELD/Validate Prototype")]
        public static void ValidatePrototypeMenu()
        {
            List<string> errors = Validate();
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("SHIELD Validation", "All automated prototype checks passed.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("SHIELD Validation Failed", string.Join("\n", errors), "OK");
            }
        }

        public static void BuildAndValidateBatch()
        {
            ShieldPrototypeBuilder.BuildPrototypeBatch();
            ValidatePrototypeBatch();
        }

        public static void ValidatePrototypeBatch()
        {
            List<string> errors = Validate();
            if (errors.Count > 0)
            {
                throw new System.InvalidOperationException("SHIELD validation failed:\n" + string.Join("\n", errors));
            }

            Debug.Log("[SHIELD_VALIDATION] PASS");
        }

        private static List<string> Validate()
        {
            var errors = new List<string>();
            RequireAsset<GameplayConfig>("Assets/SHIELD/ScriptableObjects/GameplayConfig.asset", errors);
            DifficultyConfig difficulty = RequireAsset<DifficultyConfig>("Assets/SHIELD/ScriptableObjects/DifficultyConfig.asset", errors);
            RequireAsset<FeedbackConfig>("Assets/SHIELD/ScriptableObjects/FeedbackConfig.asset", errors);
            RequireAsset<GameObject>("Assets/SHIELD/Prefabs/Gameplay/Core.prefab", errors);
            RequireAsset<GameObject>("Assets/SHIELD/Prefabs/Gameplay/Shield.prefab", errors);
            RequireAsset<GameObject>("Assets/SHIELD/Prefabs/Gameplay/Projectile.prefab", errors);
            RequireAsset<GameObject>("Assets/SHIELD/Prefabs/VFX/BlockBurstVFX.prefab", errors);
            RequireAsset<SceneAsset>(HomeScenePath, errors);
            RequireAsset<SceneAsset>(GameScenePath, errors);

            if (difficulty != null && !difficulty.ValidateConfiguration(out string configMessage))
            {
                errors.Add(configMessage);
            }

            ValidateUpdateRule(errors);
            ValidateBuildSettings(errors);
            if (File.Exists(GameScenePath))
            {
                ValidateGameScene(errors);
            }

            return errors;
        }

        private static T RequireAsset<T>(string path, List<string> errors) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) errors.Add("Missing asset: " + path);
            return asset;
        }

        private static void ValidateUpdateRule(List<string> errors)
        {
            string[] scripts = Directory.GetFiles("Assets/SHIELD/Scripts", "*.cs", SearchOption.AllDirectories);
            var updatePattern = new Regex(@"\b(?:public|private|protected|internal)?\s*(?:static\s+)?void\s+(Update|LateUpdate|FixedUpdate)\s*\(");
            for (int i = 0; i < scripts.Length; i++)
            {
                string source = File.ReadAllText(scripts[i]);
                MatchCollection matches = updatePattern.Matches(source);
                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    string method = matches[matchIndex].Groups[1].Value;
                    bool allowed = method == "Update" && Path.GetFileName(scripts[i]) == "InputManager.cs";
                    if (!allowed) errors.Add("Forbidden frame method " + method + " in " + scripts[i]);
                }
            }
        }

        private static void ValidateBuildSettings(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length < 2 || scenes[0].path != HomeScenePath || scenes[1].path != GameScenePath)
            {
                errors.Add("Build Settings must contain Home then Game.");
            }
        }

        private static void ValidateGameScene(List<string> errors)
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            int gameManagers = 0;
            int inputManagers = 0;
            int audioListeners = 0;
            int rigidbodies = 0;
            int colliders = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Component[] components = roots[i].GetComponentsInChildren<Component>(true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component == null)
                    {
                        errors.Add("Missing script in Game scene under " + roots[i].name);
                        continue;
                    }

                    if (component is GameManager) gameManagers++;
                    if (component is InputManager) inputManagers++;
                    if (component is AudioListener) audioListeners++;
                    if (component is Rigidbody2D) rigidbodies++;
                    if (component is Collider2D) colliders++;
                }
            }

            if (gameManagers != 1) errors.Add("Game scene must contain exactly one GameManager.");
            if (inputManagers != 1) errors.Add("Game scene must contain exactly one InputManager.");
            if (audioListeners != 1) errors.Add("Game scene must contain exactly one AudioListener.");
            if (rigidbodies != 0 || colliders != 0) errors.Add("Game scene contains gameplay physics components.");
        }
    }
}
#endif
