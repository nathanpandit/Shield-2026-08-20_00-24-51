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
        private const string DuoGameScenePath = "Assets/SHIELD/Scenes/DuoGame.unity";

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
            GameplayConfig gameplay = RequireAsset<GameplayConfig>("Assets/SHIELD/ScriptableObjects/GameplayConfig.asset", errors);
            DifficultyConfig difficulty = RequireAsset<DifficultyConfig>("Assets/SHIELD/ScriptableObjects/DifficultyConfig.asset", errors);
            RequireAsset<FeedbackConfig>("Assets/SHIELD/ScriptableObjects/FeedbackConfig.asset", errors);
            RequireAsset<GameObject>("Assets/SHIELD/Prefabs/Gameplay/Core.prefab", errors);
            RequireAsset<GameObject>("Assets/SHIELD/Prefabs/Gameplay/Shield.prefab", errors);
            RequireAsset<GameObject>("Assets/SHIELD/Prefabs/Gameplay/Projectile.prefab", errors);
            GameObject blockBurstPrefab = RequireAsset<GameObject>("Assets/SHIELD/Prefabs/VFX/BlockBurstVFX.prefab", errors);
            RequireAsset<SceneAsset>(HomeScenePath, errors);
            RequireAsset<SceneAsset>(GameScenePath, errors);
            RequireAsset<SceneAsset>(DuoGameScenePath, errors);

            if (difficulty != null && !difficulty.ValidateConfiguration(out string configMessage))
            {
                errors.Add(configMessage);
            }

            if (gameplay != null)
            {
                ValidateProjectileTypeConfiguration(gameplay, errors);
            }

            if (blockBurstPrefab != null)
            {
                ParticleSystemRenderer renderer = blockBurstPrefab.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    errors.Add("Block burst particle renderer must use a material that supports projectile tint colors.");
                }
            }

            ValidateUpdateRule(errors);
            ValidateBuildSettings(errors);
            if (File.Exists(GameScenePath))
            {
                ValidateGameScene(errors);
            }

            if (File.Exists(DuoGameScenePath))
            {
                ValidateDuoGameScene(errors);
            }

            return errors;
        }

        private static void ValidateProjectileTypeConfiguration(GameplayConfig gameplay, List<string> errors)
        {
            float totalWeight = 0f;
            for (int i = 0; i <= (int)ProjectileType.Orange; i++)
            {
                totalWeight += gameplay.GetProjectileWeight((ProjectileType)i);
            }

            if (totalWeight <= Mathf.Epsilon)
            {
                errors.Add("Projectile type weights must contain at least one positive value.");
            }

            if (gameplay.specialProjectileWarmupCount < 0)
            {
                errors.Add("Projectile type warmup count cannot be negative.");
            }

            if (gameplay.greenProjectileSpeedMultiplier < 1f)
            {
                errors.Add("Green projectile speed multiplier must be at least 1.0.");
            }

            if (gameplay.blueProjectileSpeedMultiplier <= 0f || gameplay.blueProjectileSpeedMultiplier > 1f)
            {
                errors.Add("Blue projectile speed multiplier must be greater than zero and at most 1.0.");
            }

            if (gameplay.duoCrossArenaImpactGap < 0f)
            {
                errors.Add("DUO cross-arena impact gap cannot be negative.");
            }

            if (gameplay.greenProtectionDuration <= 0f ||
                gameplay.blueSlowDuration <= 0f ||
                gameplay.purpleReverseDuration <= 0f ||
                gameplay.orangeSwitchDuration <= 0f)
            {
                errors.Add("Projectile effect and path durations must be greater than zero.");
            }

            float minimumOrangeSwitchRadius = gameplay.shieldRadiusNormalized
                + gameplay.shieldThicknessNormalized * 0.5f
                + gameplay.projectileSizeNormalized;
            if (gameplay.orangeSwitchRadiusNormalized <= minimumOrangeSwitchRadius ||
                gameplay.orangeSwitchRadiusNormalized >= 0.5f)
            {
                errors.Add("Orange switch radius must be outside the shield and inside the projectile spawn boundary.");
            }
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
            if (scenes.Length < 3 ||
                scenes[0].path != HomeScenePath ||
                scenes[1].path != GameScenePath ||
                scenes[2].path != DuoGameScenePath)
            {
                errors.Add("Build Settings must contain Home, Game, then DuoGame.");
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

        private static void ValidateDuoGameScene(List<string> errors)
        {
            Scene scene = EditorSceneManager.OpenScene(DuoGameScenePath, OpenSceneMode.Single);
            int duoManagers = 0;
            int soloManagers = 0;
            int sessions = 0;
            int layouts = 0;
            int spawners = 0;
            int pools = 0;
            int scores = 0;
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
                        errors.Add("Missing script in DuoGame scene under " + roots[i].name);
                        continue;
                    }

                    if (component is DuoGameManager) duoManagers++;
                    if (component is GameManager) soloManagers++;
                    if (component is DuoArenaSession) sessions++;
                    if (component is ArenaLayout) layouts++;
                    if (component is ProjectileSpawner) spawners++;
                    if (component is ProjectilePool) pools++;
                    if (component is ScoreManager) scores++;
                    if (component is InputManager) inputManagers++;
                    if (component is AudioListener) audioListeners++;
                    if (component is Rigidbody2D) rigidbodies++;
                    if (component is Collider2D) colliders++;
                }
            }

            if (duoManagers != 1) errors.Add("DuoGame scene must contain exactly one DuoGameManager.");
            if (soloManagers != 0) errors.Add("DuoGame scene must not contain a SOLO GameManager.");
            if (sessions != 2) errors.Add("DuoGame scene must contain exactly two DuoArenaSession components.");
            if (layouts != 2 || spawners != 2 || pools != 2 || scores != 2)
            {
                errors.Add("DuoGame scene must contain two complete arena simulations.");
            }
            if (inputManagers != 1) errors.Add("DuoGame scene must contain exactly one InputManager.");
            if (audioListeners != 1) errors.Add("DuoGame scene must contain exactly one AudioListener.");
            if (rigidbodies != 0 || colliders != 0) errors.Add("DuoGame scene contains gameplay physics components.");
        }
    }
}
#endif
