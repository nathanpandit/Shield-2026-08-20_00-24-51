using System;
using UnityEngine;

namespace ShieldGame
{
    /// <summary>
    /// The projectile simulation only needs this small session boundary. SOLO is one
    /// session; DUO owns two independent implementations of the same contract.
    /// </summary>
    public interface IGameplaySession
    {
        event Action<float> GameplayTick;

        GameState State { get; }
        ShieldController Shield { get; }
        bool BlueSlowActive { get; }
        float ProjectileSpeedMultiplier { get; }

        void HandleProjectileBlocked(ProjectileType projectileType, Vector3 impactPosition);
        void HandleProjectileMissed(ProjectileType projectileType, AttackDirection direction, Vector3 impactPosition);
        void PlayOrangeSwitchCue();
    }
}
