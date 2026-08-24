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

        public static event Action OnGameplayTap;

        private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>(8);

        public void Configure(GameManager manager, ShieldController shield)
        {
            gameManager = manager;
            shieldController = shield;
        }

        // This is deliberately the only Update method in custom SHIELD code.
        // Input is resolved before the central gameplay tick so a same-frame tap wins an impact tie.
        private void Update()
        {
            if (gameManager == null)
            {
                return;
            }

            if (gameManager.State == GameState.Playing && GameplayTapBeganThisFrame())
            {
                gameManager.RotateShieldForTap();
                OnGameplayTap?.Invoke();
            }

            ReadDebugHotkeys();
            gameManager.Tick(Time.deltaTime);
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
