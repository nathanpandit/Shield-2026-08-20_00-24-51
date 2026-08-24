#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShieldGame.Editor
{
    public static class ShieldPrototypeBuilder
    {
        private const string Root = "Assets/SHIELD";
        private const string GameplayConfigPath = Root + "/ScriptableObjects/GameplayConfig.asset";
        private const string DifficultyConfigPath = Root + "/ScriptableObjects/DifficultyConfig.asset";
        private const string FeedbackConfigPath = Root + "/ScriptableObjects/FeedbackConfig.asset";
        private const string CircleSpritePath = Root + "/Art/CoreCircle.png";
        private const string SquareSpritePath = Root + "/Art/WhiteSquare.png";
        private const string FontPath = Root + "/Art/ShieldFont.asset";
        private const string TmpSettingsPath = Root + "/Resources/TMP Settings.asset";
        private const string LineMaterialPath = Root + "/Materials/NeonSprite.mat";
        private const string CorePrefabPath = Root + "/Prefabs/Gameplay/Core.prefab";
        private const string ShieldPrefabPath = Root + "/Prefabs/Gameplay/Shield.prefab";
        private const string ProjectilePrefabPath = Root + "/Prefabs/Gameplay/Projectile.prefab";
        private const string BlockVfxPrefabPath = Root + "/Prefabs/VFX/BlockBurstVFX.prefab";
        private const string HomeScenePath = Root + "/Scenes/Home.unity";
        private const string GameScenePath = Root + "/Scenes/Game.unity";
        private const string DuoGameScenePath = Root + "/Scenes/DuoGame.unity";

        [MenuItem("Tools/SHIELD/Build Prototype")]
        public static void BuildPrototypeMenu()
        {
            bool existingScenes = AssetDatabase.LoadAssetAtPath<SceneAsset>(HomeScenePath) != null ||
                                  AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) != null ||
                                  AssetDatabase.LoadAssetAtPath<SceneAsset>(DuoGameScenePath) != null;
            if (existingScenes && !EditorUtility.DisplayDialog(
                    "Rebuild SHIELD Prototype?",
                    "This will replace generated SHIELD scenes and prefabs. Configuration assets and their tuning values will be preserved.",
                    "Rebuild",
                    "Cancel"))
            {
                return;
            }

            BuildPrototype(false);
        }

        public static void BuildPrototypeBatch()
        {
            BuildPrototype(true);
        }

        private static void BuildPrototype(bool batchMode)
        {
            try
            {
                EnsureFolders();
                GameplayConfig gameplay = GetOrCreateGameplayConfig();
                DifficultyConfig difficulty = GetOrCreateDifficultyConfig();
                FeedbackConfig feedback = GetOrCreateFeedbackConfig();
                if (!difficulty.ValidateConfiguration(out string validationMessage))
                {
                    throw new System.InvalidOperationException(validationMessage);
                }

                Sprite circle = GetOrCreateSprite(CircleSpritePath, true);
                Sprite square = GetOrCreateSprite(SquareSpritePath, false);
                TMP_FontAsset font = GetOrCreateFont();
                Material lineMaterial = GetOrCreateLineMaterial();

                GameObject corePrefab = BuildCorePrefab(circle);
                GameObject shieldPrefab = BuildShieldPrefab(gameplay, feedback, lineMaterial);
                GameObject projectilePrefab = BuildProjectilePrefab(square);
                GameObject blockVfxPrefab = BuildBlockVfxPrefab(feedback, lineMaterial);

                BuildHomeScene(font, square, feedback);
                BuildGameScene(gameplay, difficulty, feedback, font, square, corePrefab, shieldPrefab, projectilePrefab, blockVfxPrefab);
                BuildDuoGameScene(gameplay, difficulty, feedback, font, square, corePrefab, shieldPrefab, projectilePrefab, blockVfxPrefab);
                ConfigureProject();
                RemoveUnusedTemplateAssets();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("SHIELD prototype built successfully. Open Assets/SHIELD/Scenes/Home.unity and press Play.");
                if (!batchMode)
                {
                    EditorUtility.DisplayDialog("SHIELD", "Prototype built successfully. Home.unity is ready to play.", "Great");
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                if (!batchMode)
                {
                    EditorUtility.DisplayDialog("SHIELD Build Failed", exception.Message, "OK");
                }

                throw;
            }
        }

        private static GameplayConfig GetOrCreateGameplayConfig()
        {
            GameplayConfig asset = AssetDatabase.LoadAssetAtPath<GameplayConfig>(GameplayConfigPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<GameplayConfig>();
            AssetDatabase.CreateAsset(asset, GameplayConfigPath);
            return asset;
        }

        private static DifficultyConfig GetOrCreateDifficultyConfig()
        {
            DifficultyConfig asset = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(DifficultyConfigPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<DifficultyConfig>();
            asset.ApplyRecommendedDefaults();
            AssetDatabase.CreateAsset(asset, DifficultyConfigPath);
            return asset;
        }

        private static FeedbackConfig GetOrCreateFeedbackConfig()
        {
            FeedbackConfig asset = AssetDatabase.LoadAssetAtPath<FeedbackConfig>(FeedbackConfigPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<FeedbackConfig>();
            AssetDatabase.CreateAsset(asset, FeedbackConfigPath);
            return asset;
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                Root, Root + "/Art", Root + "/Materials", Root + "/Resources", Root + "/Scenes", Root + "/ScriptableObjects",
                Root + "/Prefabs", Root + "/Prefabs/Gameplay", Root + "/Prefabs/VFX"
            };
            for (int i = 0; i < folders.Length; i++)
            {
                if (!Directory.Exists(folders[i])) Directory.CreateDirectory(folders[i]);
            }

            AssetDatabase.Refresh();
        }

        private static Sprite GetOrCreateSprite(string path, bool circle)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.47f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f;
                    if (circle)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        alpha = Mathf.Clamp01(radius - distance + 1f);
                    }

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = size;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static TMP_FontAsset GetOrCreateFont()
        {
            TMP_Settings settings = EnsureTmpSettings();
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null)
            {
                TMP_Settings.defaultFontAsset = existing;
                EditorUtility.SetDirty(settings);
                return existing;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset("Arial", "Regular");
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset("Segoe UI", "Regular");
            }
            if (fontAsset == null)
            {
                throw new System.InvalidOperationException("Could not create the SHIELD UI font from Arial or Segoe UI.");
            }

            fontAsset.name = "ShieldFont";
            const string requiredCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789: []/.-+%";
            fontAsset.TryAddCharacters(requiredCharacters, out _);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(fontAsset, FontPath);
            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            for (int i = 0; i < atlasTextures.Length; i++)
            {
                if (atlasTextures[i] != null && !AssetDatabase.Contains(atlasTextures[i]))
                {
                    AssetDatabase.AddObjectToAsset(atlasTextures[i], fontAsset);
                }
            }

            EditorUtility.SetDirty(fontAsset);
            TMP_Settings.defaultFontAsset = fontAsset;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static TMP_Settings EnsureTmpSettings()
        {
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, TmpSettingsPath);
                var serializedSettings = new SerializedObject(settings);
                SerializedProperty version = serializedSettings.FindProperty("assetVersion");
                if (version != null) version.stringValue = "2";
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            if (TMP_Settings.LoadDefaultSettings() == null)
            {
                throw new System.InvalidOperationException("Could not initialize TextMeshPro settings.");
            }

            return settings;
        }

        private static Material GetOrCreateLineMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new System.InvalidOperationException("Built-in Sprites/Default shader was not found.");
            var material = new Material(shader) { name = "NeonSprite" };
            AssetDatabase.CreateAsset(material, LineMaterialPath);
            return material;
        }

        private static GameObject BuildCorePrefab(Sprite circle)
        {
            var root = new GameObject("Core");
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = circle;
            renderer.sortingOrder = 2;
            CoreController controller = root.AddComponent<CoreController>();
            controller.Configure(renderer);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CorePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildShieldPrefab(GameplayConfig gameplay, FeedbackConfig feedback, Material material)
        {
            var root = new GameObject("ShieldPivot");
            var arcObject = new GameObject("ShieldArcVisual");
            arcObject.transform.SetParent(root.transform, false);
            LineRenderer line = arcObject.AddComponent<LineRenderer>();
            line.material = material;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.TransformZ;
            line.sortingOrder = 4;
            ShieldController controller = root.AddComponent<ShieldController>();
            controller.Configure(root.transform, line, gameplay, feedback);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ShieldPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildProjectilePrefab(Sprite square)
        {
            var root = new GameObject("Projectile");
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.sortingOrder = 3;
            ProjectileController controller = root.AddComponent<ProjectileController>();
            controller.Configure(renderer);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildBlockVfxPrefab(FeedbackConfig feedback, Material material)
        {
            var root = new GameObject("BlockBurstVFX");
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.startLifetime = feedback.blockBurstDuration;
            main.startSpeed = feedback.blockBurstScale * 5f;
            main.startSize = feedback.blockBurstScale;
            main.startColor = feedback.shieldColor;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.04f;
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 8;
            BlockBurstVFX controller = root.AddComponent<BlockBurstVFX>();
            controller.Configure(particles, feedback);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BlockVfxPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildHomeScene(TMP_FontAsset font, Sprite square, FeedbackConfig feedback)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(feedback.backgroundColor);
            RectTransform safeArea = CreateCanvas("Canvas");

            TMP_Text title = CreateText(safeArea, "TitleText", "SHIELD", font, 106, new Vector2(0.5f, 0.79f), new Vector2(760f, 160f), feedback.shieldColor);
            TMP_Text best = CreateText(safeArea, "SoloBestScoreText", "SOLO BEST: 0", font, 42, new Vector2(0.5f, 0.66f), new Vector2(700f, 80f), feedback.scoreColor);
            TMP_Text duoBest = CreateText(safeArea, "DuoBestScoreText", "DUO BEST: 0", font, 42, new Vector2(0.5f, 0.61f), new Vector2(700f, 80f), feedback.scoreColor);
            Button play = CreateButton(safeArea, "SoloButton", "SOLO", font, square, new Vector2(0.5f, 0.49f), new Vector2(440f, 120f), feedback.shieldColor, out _);
            Button duo = CreateButton(safeArea, "DuoButton", "DUO", font, square, new Vector2(0.5f, 0.39f), new Vector2(440f, 120f), feedback.projectileColor, out _);
            Button sound = CreateButton(safeArea, "SoundButton", "SOUND: ON", font, square, new Vector2(0.5f, 0.24f), new Vector2(400f, 90f), new Color(0.12f, 0.19f, 0.32f, 1f), out TMP_Text soundLabel);
            Button haptic = CreateButton(safeArea, "HapticButton", "HAPTIC: ON", font, square, new Vector2(0.5f, 0.16f), new Vector2(400f, 90f), new Color(0.12f, 0.19f, 0.32f, 1f), out TMP_Text hapticLabel);

            var controllerObject = new GameObject("HomeController");
            HomeUIController controller = controllerObject.AddComponent<HomeUIController>();
            controller.Configure(best, duoBest, play, duo, sound, soundLabel, haptic, hapticLabel);
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, HomeScenePath);
        }

        private static void BuildGameScene(
            GameplayConfig gameplay,
            DifficultyConfig difficultyConfig,
            FeedbackConfig feedback,
            TMP_FontAsset font,
            Sprite square,
            GameObject corePrefab,
            GameObject shieldPrefab,
            GameObject projectilePrefab,
            GameObject blockVfxPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera(feedback.backgroundColor);

            var gameplayRoot = new GameObject("GameplayRoot");
            GameObject coreObject = (GameObject)PrefabUtility.InstantiatePrefab(corePrefab);
            coreObject.transform.SetParent(gameplayRoot.transform, false);
            CoreController core = coreObject.GetComponent<CoreController>();
            GameObject shieldObject = (GameObject)PrefabUtility.InstantiatePrefab(shieldPrefab);
            shieldObject.transform.SetParent(gameplayRoot.transform, false);
            ShieldController shield = shieldObject.GetComponent<ShieldController>();
            var projectileRoot = new GameObject("ProjectileRoot");
            projectileRoot.transform.SetParent(gameplayRoot.transform, false);
            var vfxRoot = new GameObject("VFXRoot");
            vfxRoot.transform.SetParent(gameplayRoot.transform, false);
            GameObject blockObject = (GameObject)PrefabUtility.InstantiatePrefab(blockVfxPrefab);
            blockObject.transform.SetParent(vfxRoot.transform, false);
            BlockBurstVFX blockVfx = blockObject.GetComponent<BlockBurstVFX>();

            var gameRoot = new GameObject("GameRoot");
            GameManager manager = CreateManager<GameManager>(gameRoot.transform, "GameManager");
            InputManager input = CreateManager<InputManager>(gameRoot.transform, "InputManager");
            ArenaLayout arena = CreateManager<ArenaLayout>(gameRoot.transform, "ArenaLayout");
            DifficultyManager difficulty = CreateManager<DifficultyManager>(gameRoot.transform, "DifficultyManager");
            AttackDirector director = CreateManager<AttackDirector>(gameRoot.transform, "AttackDirector");
            ProjectileSpawner spawner = CreateManager<ProjectileSpawner>(gameRoot.transform, "ProjectileSpawner");
            ProjectilePool pool = CreateManager<ProjectilePool>(gameRoot.transform, "ProjectilePool");
            ScoreManager score = CreateManager<ScoreManager>(gameRoot.transform, "ScoreManager");
            ShieldAudioManager audio = CreateManager<ShieldAudioManager>(gameRoot.transform, "AudioManager");
            AudioSource musicSource = audio.gameObject.AddComponent<AudioSource>();
            AudioSource sfxSource = audio.gameObject.AddComponent<AudioSource>();
            audio.Configure(musicSource, sfxSource);
            HapticManager haptics = CreateManager<HapticManager>(gameRoot.transform, "HapticManager");

            difficulty.Configure(difficultyConfig);
            director.Configure(difficultyConfig);
            pool.Configure(projectilePrefab.GetComponent<ProjectileController>(), projectileRoot.transform, gameplay, feedback);
            arena.Configure(camera, core, shield, gameplay, feedback);
            spawner.Configure(gameplay, difficulty, director, pool, arena);
            manager.Configure(gameplay, feedback, core, shield, pool, spawner, score, difficulty, director, audio, haptics, blockVfx);
            input.Configure(manager, shield);

            RectTransform safeArea = CreateCanvas("Canvas");
            TMP_Text activeScore = CreateText(safeArea, "ScoreText", "0", font, 42, new Vector2(0.5f, 0.5f), new Vector2(106f, 78f), Color.black);
            activeScore.enableAutoSizing = true;
            activeScore.fontSizeMin = 22f;
            activeScore.fontSizeMax = 42f;
            Button pauseButton = CreateButton(safeArea, "PauseButton", "II", font, square, new Vector2(1f, 1f), new Vector2(100f, 100f), new Color(0.12f, 0.19f, 0.32f, 0.95f), out _, new Vector2(-75f, -75f));

            GameObject pausePanel = CreateOverlay(safeArea, "PauseOverlay", square);
            CreateText(pausePanel.transform, "PauseTitle", "PAUSED", font, 70, new Vector2(0.5f, 0.64f), new Vector2(700f, 120f), feedback.scoreColor);
            Button resume = CreateButton(pausePanel.transform, "ResumeButton", "RESUME", font, square, new Vector2(0.5f, 0.48f), new Vector2(430f, 115f), feedback.shieldColor, out _);
            Button pauseHome = CreateButton(pausePanel.transform, "HomeButton", "HOME", font, square, new Vector2(0.5f, 0.36f), new Vector2(430f, 100f), new Color(0.12f, 0.19f, 0.32f, 1f), out _);

            GameObject gameOverPanel = CreateOverlay(safeArea, "GameOverOverlay", square);
            CreateText(gameOverPanel.transform, "GameOverTitle", "GAME OVER", font, 74, new Vector2(0.5f, 0.69f), new Vector2(760f, 130f), feedback.shieldColor);
            TMP_Text finalScore = CreateText(gameOverPanel.transform, "ScoreText", "SCORE: 0", font, 50, new Vector2(0.5f, 0.56f), new Vector2(700f, 90f), feedback.scoreColor);
            TMP_Text bestScore = CreateText(gameOverPanel.transform, "BestScoreText", "BEST: 0", font, 44, new Vector2(0.5f, 0.49f), new Vector2(700f, 90f), feedback.scoreColor);
            TMP_Text newBest = CreateText(gameOverPanel.transform, "NewBestText", "NEW BEST", font, 38, new Vector2(0.5f, 0.42f), new Vector2(700f, 80f), feedback.projectileColor);
            Button restart = CreateButton(gameOverPanel.transform, "RestartButton", "RESTART", font, square, new Vector2(0.5f, 0.31f), new Vector2(450f, 115f), feedback.shieldColor, out _);
            Button gameOverHome = CreateButton(gameOverPanel.transform, "HomeButton", "HOME", font, square, new Vector2(0.5f, 0.20f), new Vector2(450f, 100f), new Color(0.12f, 0.19f, 0.32f, 1f), out _);

            GameObject debugPanel = CreateDebugPanel(safeArea, square, font, out TMP_Text debugText);
            GameUIController gameUi = safeArea.gameObject.AddComponent<GameUIController>();
            gameUi.Configure(activeScore, pauseButton, pausePanel, resume, pauseHome, gameOverPanel, finalScore, bestScore, newBest, restart, gameOverHome);
            DebugOverlayController debugOverlay = safeArea.gameObject.AddComponent<DebugOverlayController>();
            debugOverlay.Configure(debugPanel, debugText);

            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);
            newBest.gameObject.SetActive(false);
            debugPanel.SetActive(false);
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void BuildDuoGameScene(
            GameplayConfig gameplay,
            DifficultyConfig difficultyConfig,
            FeedbackConfig feedback,
            TMP_FontAsset font,
            Sprite square,
            GameObject corePrefab,
            GameObject shieldPrefab,
            GameObject projectilePrefab,
            GameObject blockVfxPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = CreateCamera(feedback.backgroundColor);
            var gameplayRoot = new GameObject("DuoGameplayRoot");
            var gameRoot = new GameObject("DuoGameRoot");

            DuoArenaBundle first = BuildDuoArena(
                1, gameplayRoot.transform, gameRoot.transform, camera, gameplay, difficultyConfig, feedback,
                corePrefab, shieldPrefab, projectilePrefab, blockVfxPrefab);
            DuoArenaBundle second = BuildDuoArena(
                2, gameplayRoot.transform, gameRoot.transform, camera, gameplay, difficultyConfig, feedback,
                corePrefab, shieldPrefab, projectilePrefab, blockVfxPrefab);

            DuoLayoutController duoLayout = CreateManager<DuoLayoutController>(gameRoot.transform, "DuoLayoutController");
            duoLayout.Configure(first.Layout, second.Layout);
            ShieldAudioManager audio = CreateManager<ShieldAudioManager>(gameRoot.transform, "AudioManager");
            AudioSource musicSource = audio.gameObject.AddComponent<AudioSource>();
            AudioSource sfxSource = audio.gameObject.AddComponent<AudioSource>();
            audio.Configure(musicSource, sfxSource);
            HapticManager haptics = CreateManager<HapticManager>(gameRoot.transform, "HapticManager");
            DuoGameManager manager = CreateManager<DuoGameManager>(gameRoot.transform, "DuoGameManager");
            manager.Configure(gameplay, feedback, first.Session, second.Session, duoLayout, audio, haptics);
            InputManager input = CreateManager<InputManager>(gameRoot.transform, "InputManager");
            input.ConfigureDuo(manager);

            RectTransform safeArea = CreateCanvas("DuoCanvas");
            RectTransform firstPanel = CreateArenaUiPanel(safeArea, "FirstArenaUI");
            RectTransform secondPanel = CreateArenaUiPanel(safeArea, "SecondArenaUI");
            TMP_Text firstScore = CreateText(firstPanel, "ScoreText", "0", font, 36, new Vector2(0.5f, 0.5f), new Vector2(90f, 60f), Color.black);
            TMP_Text secondScore = CreateText(secondPanel, "ScoreText", "0", font, 36, new Vector2(0.5f, 0.5f), new Vector2(90f, 60f), Color.black);
            Button pauseButton = CreateButton(safeArea, "PauseButton", "II", font, square, new Vector2(1f, 1f), new Vector2(82f, 82f), new Color(0.12f, 0.19f, 0.32f, 0.95f), out _, new Vector2(-56f, -56f));

            GameObject pausePanel = CreateOverlay(safeArea, "PauseOverlay", square);
            CreateText(pausePanel.transform, "PauseTitle", "PAUSED", font, 70, new Vector2(0.5f, 0.64f), new Vector2(700f, 120f), feedback.scoreColor);
            Button resume = CreateButton(pausePanel.transform, "ResumeButton", "RESUME", font, square, new Vector2(0.5f, 0.48f), new Vector2(430f, 115f), feedback.shieldColor, out _);
            Button pauseHome = CreateButton(pausePanel.transform, "HomeButton", "HOME", font, square, new Vector2(0.5f, 0.36f), new Vector2(430f, 100f), new Color(0.12f, 0.19f, 0.32f, 1f), out _);

            GameObject gameOverPanel = CreateOverlay(safeArea, "GameOverOverlay", square);
            CreateText(gameOverPanel.transform, "GameOverTitle", "DUO OVER", font, 68, new Vector2(0.5f, 0.76f), new Vector2(760f, 120f), feedback.shieldColor);
            TMP_Text firstFinal = CreateText(gameOverPanel.transform, "FirstScoreText", "P1: 0", font, 42, new Vector2(0.5f, 0.66f), new Vector2(650f, 72f), feedback.scoreColor);
            TMP_Text secondFinal = CreateText(gameOverPanel.transform, "SecondScoreText", "P2: 0", font, 42, new Vector2(0.5f, 0.60f), new Vector2(650f, 72f), feedback.scoreColor);
            TMP_Text total = CreateText(gameOverPanel.transform, "TotalScoreText", "TOTAL: 0", font, 54, new Vector2(0.5f, 0.51f), new Vector2(700f, 90f), feedback.scoreColor);
            TMP_Text duoBest = CreateText(gameOverPanel.transform, "DuoBestScoreText", "DUO BEST: 0", font, 40, new Vector2(0.5f, 0.44f), new Vector2(700f, 80f), feedback.scoreColor);
            TMP_Text newBest = CreateText(gameOverPanel.transform, "NewBestText", "NEW DUO BEST", font, 36, new Vector2(0.5f, 0.37f), new Vector2(700f, 72f), feedback.projectileColor);
            Button restart = CreateButton(gameOverPanel.transform, "RestartButton", "RESTART", font, square, new Vector2(0.5f, 0.26f), new Vector2(450f, 110f), feedback.shieldColor, out _);
            Button gameOverHome = CreateButton(gameOverPanel.transform, "HomeButton", "HOME", font, square, new Vector2(0.5f, 0.16f), new Vector2(450f, 96f), new Color(0.12f, 0.19f, 0.32f, 1f), out _);

            DuoGameUIController duoUi = safeArea.gameObject.AddComponent<DuoGameUIController>();
            duoUi.Configure(
                firstPanel, secondPanel, firstScore, secondScore, pauseButton, pausePanel, resume, pauseHome,
                gameOverPanel, firstFinal, secondFinal, total, duoBest, newBest, restart, gameOverHome);

            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);
            newBest.gameObject.SetActive(false);
            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, DuoGameScenePath);
        }

        private static DuoArenaBundle BuildDuoArena(
            int arenaNumber,
            Transform gameplayParent,
            Transform systemsParent,
            Camera camera,
            GameplayConfig gameplay,
            DifficultyConfig difficultyConfig,
            FeedbackConfig feedback,
            GameObject corePrefab,
            GameObject shieldPrefab,
            GameObject projectilePrefab,
            GameObject blockVfxPrefab)
        {
            var arenaRoot = new GameObject("Arena" + arenaNumber);
            arenaRoot.transform.SetParent(gameplayParent, false);
            GameObject coreObject = (GameObject)PrefabUtility.InstantiatePrefab(corePrefab);
            coreObject.name = "Core";
            coreObject.transform.SetParent(arenaRoot.transform, false);
            CoreController core = coreObject.GetComponent<CoreController>();
            GameObject shieldObject = (GameObject)PrefabUtility.InstantiatePrefab(shieldPrefab);
            shieldObject.name = "ShieldPivot";
            shieldObject.transform.SetParent(arenaRoot.transform, false);
            ShieldController shield = shieldObject.GetComponent<ShieldController>();
            var projectileRoot = new GameObject("ProjectileRoot");
            projectileRoot.transform.SetParent(arenaRoot.transform, false);
            GameObject blockObject = (GameObject)PrefabUtility.InstantiatePrefab(blockVfxPrefab);
            blockObject.name = "BlockBurstVFX";
            blockObject.transform.SetParent(arenaRoot.transform, false);
            BlockBurstVFX blockVfx = blockObject.GetComponent<BlockBurstVFX>();

            var systemsRoot = new GameObject("Arena" + arenaNumber + "Systems");
            systemsRoot.transform.SetParent(systemsParent, false);
            DuoArenaSession session = CreateManager<DuoArenaSession>(systemsRoot.transform, "Session");
            ArenaLayout layout = CreateManager<ArenaLayout>(systemsRoot.transform, "ArenaLayout");
            DifficultyManager difficulty = CreateManager<DifficultyManager>(systemsRoot.transform, "DifficultyManager");
            AttackDirector director = CreateManager<AttackDirector>(systemsRoot.transform, "AttackDirector");
            ProjectileSpawner spawner = CreateManager<ProjectileSpawner>(systemsRoot.transform, "ProjectileSpawner");
            ProjectilePool pool = CreateManager<ProjectilePool>(systemsRoot.transform, "ProjectilePool");
            ScoreManager score = CreateManager<ScoreManager>(systemsRoot.transform, "ScoreManager");

            difficulty.Configure(difficultyConfig);
            director.Configure(difficultyConfig);
            pool.Configure(projectilePrefab.GetComponent<ProjectileController>(), projectileRoot.transform, gameplay, feedback);
            layout.Configure(camera, core, shield, gameplay, feedback);
            spawner.Configure(gameplay, difficulty, director, pool, layout);
            session.Configure(gameplay, feedback, core, shield, pool, spawner, score, difficulty, director, blockVfx);
            return new DuoArenaBundle(session, layout);
        }

        private static RectTransform CreateArenaUiPanel(Transform parent, string name)
        {
            var panelObject = new GameObject(name, typeof(RectTransform));
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            return panel;
        }

        private sealed class DuoArenaBundle
        {
            public DuoArenaSession Session { get; }
            public ArenaLayout Layout { get; }

            public DuoArenaBundle(DuoArenaSession session, ArenaLayout layout)
            {
                Session = session;
                Layout = layout;
            }
        }

        private static T CreateManager<T>(Transform parent, string name) where T : Component
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.AddComponent<T>();
        }

        private static Camera CreateCamera(Color background)
        {
            var cameraObject = new GameObject("MainCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static RectTransform CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safeObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            RectTransform safe = safeObject.GetComponent<RectTransform>();
            safe.SetParent(canvasObject.transform, false);
            safe.anchorMin = Vector2.zero;
            safe.anchorMax = Vector2.one;
            safe.offsetMin = Vector2.zero;
            safe.offsetMax = Vector2.zero;
            return safe;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, TMP_FontAsset font, float size, Vector2 anchor, Vector2 dimensions, Color color, Vector2? anchoredPosition = null)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Sprite sprite, Vector2 anchor, Vector2 dimensions, Color color, out TMP_Text labelText, Vector2? anchoredPosition = null)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = anchoredPosition ?? Vector2.zero;
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            button.colors = colors;
            labelText = CreateText(buttonObject.transform, "Label", label, font, Mathf.Min(48f, dimensions.y * 0.42f), new Vector2(0.5f, 0.5f), dimensions, Color.white);
            return button;
        }

        private static GameObject CreateOverlay(Transform parent, string name, Sprite sprite)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = panel.GetComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(0.02f, 0.03f, 0.08f, 0.94f);
            return panel;
        }

        private static GameObject CreateDebugPanel(Transform parent, Sprite sprite, TMP_FontAsset font, out TMP_Text debugText)
        {
            var panel = new GameObject("DebugOverlay", typeof(RectTransform), typeof(Image));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0.38f);
            rect.anchorMax = new Vector2(0.62f, 0.96f);
            rect.offsetMin = new Vector2(20f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            Image image = panel.GetComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(0f, 0f, 0f, 0.80f);
            debugText = CreateText(panel.transform, "DebugText", string.Empty, font, 24, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
            RectTransform textRect = debugText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 18f);
            textRect.offsetMax = new Vector2(-18f, -18f);
            debugText.alignment = TextAlignmentOptions.TopLeft;
            return panel;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void ConfigureProject()
        {
            PlayerSettings.productName = "SHIELD";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            GraphicsSettings.defaultRenderPipeline = null;
            int originalQuality = QualitySettings.GetQualityLevel();
            string[] qualityNames = QualitySettings.names;
            for (int i = 0; i < qualityNames.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(originalQuality, false);

            Object[] projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (projectSettings.Length > 0)
            {
                var serializedSettings = new SerializedObject(projectSettings[0]);
                SerializedProperty inputHandler = serializedSettings.FindProperty("activeInputHandler");
                if (inputHandler != null)
                {
                    inputHandler.intValue = 0;
                    serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(HomeScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
                new EditorBuildSettingsScene(DuoGameScenePath, true)
            };
        }

        private static void RemoveUnusedTemplateAssets()
        {
            string[] templateAssets =
            {
                "Assets/DefaultVolumeProfile.asset",
                "Assets/InputSystem_Actions.inputactions",
                "Assets/UniversalRenderPipelineGlobalSettings.asset",
                "Assets/Scenes/SampleScene.unity",
                "Assets/Settings/Lit2DSceneTemplate.scenetemplate",
                "Assets/Settings/Renderer2D.asset",
                "Assets/Settings/UniversalRP.asset",
                "Assets/Settings/Scenes/URP2DSceneTemplate.unity"
            };
            for (int i = 0; i < templateAssets.Length; i++)
            {
                if (AssetDatabase.LoadMainAssetAtPath(templateAssets[i]) != null)
                {
                    AssetDatabase.DeleteAsset(templateAssets[i]);
                }
            }
        }
    }
}
#endif
