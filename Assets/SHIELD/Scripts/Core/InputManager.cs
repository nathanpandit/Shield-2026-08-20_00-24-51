using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShieldGame
{
    public sealed class InputManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ShieldController shieldController;
        [SerializeField] private DuoGameManager duoGameManager;

        public static event Action OnGameplayTap;

        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>(8);

        public void Configure(GameManager manager, ShieldController shield)
        {
            gameManager = manager;
            shieldController = shield;
        }

        public void ConfigureDuo(DuoGameManager manager)
        {
            duoGameManager = manager;
            gameManager = null;
            shieldController = null;
        }

        // This is deliberately the only Update method in custom SHIELD code.
        // Input is resolved before the central gameplay tick so a same-frame tap wins an impact tie.
        private void Update()
        {
            if (duoGameManager != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    duoGameManager.TogglePause();
                }

                UpdateDuoInput();
                duoGameManager.Tick(Time.deltaTime);
                return;
            }

            if (gameManager == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                gameManager.TogglePause();
            }

            if (gameManager.State == GameState.Playing && GameplayTapBeganThisFrame())
            {
                gameManager.RotateShieldForTap();
                OnGameplayTap?.Invoke();
            }

            ReadDebugHotkeys();
            gameManager.Tick(Time.deltaTime);
        }

        private void UpdateDuoInput()
        {
            if (duoGameManager.State == GameState.Playing)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Began || IsTouchOverUi(touch))
                    {
                        continue;
                    }

                    duoGameManager.RotateArena(duoGameManager.Layout.GetArenaIndex(touch.position));
                    OnGameplayTap?.Invoke();
                }

                // DUO deliberately uses different keyboard groups for the two boards.
                // Space plus either arrow in the same frame can rotate both boards.
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    duoGameManager.RotateArena(GetDuoArenaIndex(KeyCode.Space));
                    OnGameplayTap?.Invoke();
                }

                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    duoGameManager.RotateArena(GetDuoArenaIndex(KeyCode.LeftArrow));
                    OnGameplayTap?.Invoke();
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.R))
            {
                duoGameManager.BeginRun();
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                duoGameManager.GrantDebugGreenProtectionToAll();
            }
#else
            if (duoGameManager.DebugFeaturesAvailable && Input.GetKeyDown(KeyCode.G))
            {
                duoGameManager.GrantDebugGreenProtectionToAll();
            }
#endif
        }

        public static int GetDuoArenaIndex(KeyCode key)
        {
            if (key == KeyCode.Space) return 0;
            if (key == KeyCode.LeftArrow || key == KeyCode.RightArrow) return 1;
            return -1;
        }

        private static bool GameplayTapBeganThisFrame()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                {
                    continue;
                }

                if (!IsTouchOverUi(touch))
                {
                    return true;
                }
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        private static bool IsTouchOverUi(Touch touch)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (eventSystem.IsPointerOverGameObject(touch.fingerId))
            {
                return true;
            }

            // The direct raycast also works when InputManager runs before the EventSystem
            // has updated its pointer cache for a newly-began touch.
            var pointerData = new PointerEventData(eventSystem) { position = touch.position };
            UiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, UiRaycastResults);
            bool overUi = UiRaycastResults.Count > 0;
            UiRaycastResults.Clear();
            return overUi;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ReadEditorHotkeys()
        {
            ReadDevelopmentHotkeys();
        }

        private void ReadDebugHotkeys()
        {
#if UNITY_EDITOR
            ReadEditorHotkeys();
#elif DEVELOPMENT_BUILD
            ReadDevelopmentHotkeys();
#else
            if (gameManager.DebugFeaturesAvailable)
            {
                ReadDevelopmentHotkeys();
            }
#endif
        }

        private void ReadDevelopmentHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.R)) gameManager.BeginRun();
            if (Input.GetKeyDown(KeyCode.I)) gameManager.ToggleDebugInvincibility();
            if (Input.GetKeyDown(KeyCode.G)) gameManager.GrantDebugGreenProtection();
            if (Input.GetKeyDown(KeyCode.Alpha1)) gameManager.ForceNextAttack(AttackDirection.Top);
            if (Input.GetKeyDown(KeyCode.Alpha2)) gameManager.ForceNextAttack(AttackDirection.Right);
            if (Input.GetKeyDown(KeyCode.Alpha3)) gameManager.ForceNextAttack(AttackDirection.Bottom);
            if (Input.GetKeyDown(KeyCode.Alpha4)) gameManager.ForceNextAttack(AttackDirection.Left);
            if (Input.GetKeyDown(KeyCode.Alpha5)) gameManager.ForceNextProjectileType(ProjectileType.Yellow);
            if (Input.GetKeyDown(KeyCode.Alpha6)) gameManager.ForceNextProjectileType(ProjectileType.Green);
            if (Input.GetKeyDown(KeyCode.Alpha7)) gameManager.ForceNextProjectileType(ProjectileType.Blue);
            if (Input.GetKeyDown(KeyCode.Alpha8)) gameManager.ForceNextProjectileType(ProjectileType.Purple);
            if (Input.GetKeyDown(KeyCode.Alpha9)) gameManager.ForceNextProjectileType(ProjectileType.Red);
            if (Input.GetKeyDown(KeyCode.Alpha0)) gameManager.ForceNextProjectileType(ProjectileType.Orange);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) gameManager.AdjustDebugScore(-1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) gameManager.AdjustDebugScore(1);
            if (Input.GetKeyDown(KeyCode.F1)) gameManager.ToggleDebugOverlay();
        }
    }
}
