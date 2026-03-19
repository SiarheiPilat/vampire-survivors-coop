using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using VampireSurvivors.Components;

namespace VampireSurvivors.Systems
{
    /// <summary>
    /// Implements hold-to-revive for co-op:
    ///   - Living player holds Interact (E / I / gamepad South) for ReviveDuration seconds
    ///     while within ReviveRadius of a downed teammate.
    ///   - On completion: Downed is removed, HP restored to 50% MaxHp, 2 s iframes granted.
    ///
    /// Dev fallback: E = P0 revive, I = P1 revive. Gamepad: South button (A/Cross).
    /// Progress is tracked via ReviveProgress component on the downed entity.
    /// Not Burst-compiled — uses managed Input System APIs.
    /// </summary>
    [UpdateAfter(typeof(PlayerInputSystem))]
    public partial struct ReviveSystem : ISystem
    {
        public const float ReviveRadius   = 1.5f;
        public const float ReviveDuration = 2.0f;

        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb          = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var downedQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag, Downed, LocalTransform>()
                .Build();

            if (downedQuery.IsEmpty)
            {
                // No downed players — cancel any stale ReviveProgress (safety)
                foreach (var (_, entity) in
                    SystemAPI.Query<RefRO<ReviveProgress>>().WithEntityAccess())
                    ecb.RemoveComponent<ReviveProgress>(entity);
                return;
            }

            var downedEntities   = downedQuery.ToEntityArray(Allocator.Temp);
            var downedTransforms = downedQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var keyboard = Keyboard.current;
            float dt     = SystemAPI.Time.DeltaTime;

            // ── For each living player, determine if they're pressing Interact ──
            foreach (var (index, assignedDevice, transform, reviverEntity) in
                SystemAPI.Query<RefRO<PlayerIndex>, RefRO<AssignedDeviceId>, RefRO<LocalTransform>>()
                    .WithAll<PlayerTag>().WithNone<Downed>().WithEntityAccess())
            {
                int  i        = index.ValueRO.Value;
                int  deviceId = assignedDevice.ValueRO.Value;
                bool pressing = false;

                if (deviceId != 0)
                {
                    var device = InputSystem.GetDeviceById(deviceId);
                    if (device is Gamepad gp) pressing = gp.buttonSouth.isPressed;
                }
                else
                {
                    if (keyboard != null)
                    {
                        pressing |= (i == 0 && keyboard.eKey.isPressed);
                        pressing |= (i == 1 && keyboard.iKey.isPressed);
                    }
                    if (Gamepad.all.Count > i)
                        pressing |= Gamepad.all[i].buttonSouth.isPressed;
                }

                // Find nearest downed player within ReviveRadius
                int   nearestIdx  = -1;
                float nearestDist = float.MaxValue;
                for (int d = 0; d < downedEntities.Length; d++)
                {
                    float dist = math.distance(transform.ValueRO.Position.xy,
                                               downedTransforms[d].Position.xy);
                    if (dist <= ReviveRadius && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIdx  = d;
                    }
                }

                // Cancel revive for any downed player this reviver was working on
                // if they stepped away or released Interact
                for (int d = 0; d < downedEntities.Length; d++)
                {
                    if (d == nearestIdx && pressing) continue; // still valid — skip cancel

                    if (!SystemAPI.HasComponent<ReviveProgress>(downedEntities[d])) continue;
                    var existing = SystemAPI.GetComponent<ReviveProgress>(downedEntities[d]);
                    if (existing.Reviver == reviverEntity)
                        ecb.RemoveComponent<ReviveProgress>(downedEntities[d]);
                }

                if (!pressing || nearestIdx < 0) continue;

                var downedEntity = downedEntities[nearestIdx];

                if (SystemAPI.HasComponent<ReviveProgress>(downedEntity))
                {
                    var prog = SystemAPI.GetComponent<ReviveProgress>(downedEntity);
                    if (prog.Reviver != reviverEntity) continue; // another player has priority

                    prog.Timer += dt;

                    if (prog.Timer >= ReviveDuration)
                    {
                        // ── Revive! ──
                        var health    = SystemAPI.GetComponent<Health>(downedEntity);
                        health.Current = health.Max / 2;
                        ecb.SetComponent(downedEntity, health);
                        ecb.RemoveComponent<Downed>(downedEntity);
                        ecb.RemoveComponent<ReviveProgress>(downedEntity);
                        // Grant brief iframes so the revived player isn't immediately downed again
                        ecb.SetComponent(downedEntity, new Invincible { Timer = 2.0f });

                        int revivedIdx = SystemAPI.GetComponent<PlayerIndex>(downedEntity).Value;
                        Debug.Log($"[ReviveSystem] P{i} revived P{revivedIdx}!");
                    }
                    else
                    {
                        ecb.SetComponent(downedEntity, prog);
                    }
                }
                else
                {
                    // Start tracking revive progress
                    ecb.AddComponent(downedEntity, new ReviveProgress { Reviver = reviverEntity, Timer = 0f });
                    int revivedIdx = SystemAPI.GetComponent<PlayerIndex>(downedEntity).Value;
                    Debug.Log($"[ReviveSystem] P{i} beginning revive of P{revivedIdx}...");
                }
            }

            downedEntities.Dispose();
            downedTransforms.Dispose();
        }
    }
}
