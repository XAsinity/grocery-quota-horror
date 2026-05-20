using GroceryQuotaHorror.Bootstrap;
using System.Collections.Generic;
using GroceryQuotaHorror.Core;
using GroceryQuotaHorror.Data;
using GroceryQuotaHorror.Player;
using Unity.Netcode;
using UnityEngine;

namespace GroceryQuotaHorror.Monsters
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MonsterController : NetworkBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;

        private readonly NetworkVariable<int> definitionIndex = new(-1);

        private GameContentDatabase contentDatabase;
        private List<Transform> patrolPoints;
        private int patrolIndex;
        private int localDefinitionIndex = -1;
        private float pausedUntil;
        private float attackCooldown;

        private bool IsOfflineLocalMode => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkBootstrap.LocalOfflineMode;

        public void Initialize(int configIndex, GameContentDatabase content, IReadOnlyList<Transform> scenePatrolPoints)
        {
            localDefinitionIndex = configIndex;
            if (!NetworkObject.IsSpawned || IsServer)
            {
                definitionIndex.Value = configIndex;
            }

            contentDatabase = content;
            patrolPoints = new List<Transform>(scenePatrolPoints);
            ApplyVisuals();
        }

        public override void OnNetworkSpawn()
        {
            if (contentDatabase == null)
            {
                contentDatabase = NightGameManager.Instance != null ? NightGameManager.Instance.ContentDatabase : GameRuntime.Content;
            }

            ApplyVisuals();
        }

        private void Update()
        {
            var activeDefinitionIndex = GetActiveDefinitionIndex();
            if ((!IsServer && !IsOfflineLocalMode) || contentDatabase == null || activeDefinitionIndex < 0 || GameRuntime.Balance == null)
            {
                return;
            }

            attackCooldown -= Time.deltaTime;
            if (Time.time < pausedUntil)
            {
                return;
            }

            var definition = contentDatabase.monsterPool[activeDefinitionIndex];
            var target = FindTarget(definition);
            if (target == null)
            {
                Patrol(definition.moveSpeed);
                return;
            }

            var targetPosition = target.AiTargetPosition;
            var flat = targetPosition - transform.position;
            flat.y = 0f;
            transform.position += flat.normalized * definition.moveSpeed * Time.deltaTime;
            transform.forward = Vector3.Lerp(transform.forward, flat.normalized, GameRuntime.Balance.monster.turnSmoothing);

            if (flat.magnitude <= definition.attackRange && attackCooldown <= 0f)
            {
                var attackCollider = GetComponent<Collider>();
                var didThrow = target.TryBeginPrototypeMonsterThrow(
                    flat.normalized,
                    targetPosition + Vector3.up,
                    attackCollider);

                if (didThrow)
                {
                    PauseAfterThrow(flat.normalized);
                    attackCooldown = Mathf.Max(definition.attackCooldown, GameRuntime.Balance.monster.prototypeThrowPauseSeconds);
                }
                else
                {
                    target.ApplyDamage();
                    attackCooldown = definition.attackCooldown;
                }
            }
        }

        private PlayerController FindTarget(MonsterDefinition definition)
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            PlayerController best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < players.Length; i++)
            {
                if (players[i].IsDowned || players[i].IsHidden)
                {
                    continue;
                }

                var distance = Vector3.Distance(transform.position, players[i].AiTargetPosition);
                if (distance < bestDistance && distance <= definition.chaseRange)
                {
                    bestDistance = distance;
                    best = players[i];
                }
            }

            return best;
        }

        private void PauseAfterThrow(Vector3 throwDirection)
        {
            var tuning = GameRuntime.Balance.monster;
            pausedUntil = Time.time + Mathf.Max(0f, tuning.prototypeThrowPauseSeconds);

            var backoff = Vector3.ProjectOnPlane(throwDirection, Vector3.up);
            if (backoff.sqrMagnitude > 0.0001f)
            {
                transform.position -= backoff.normalized * Mathf.Max(0f, tuning.prototypeThrowBackoffDistance);
            }
        }

        private void Patrol(float speed)
        {
            if (patrolPoints == null || patrolPoints.Count == 0 || GameRuntime.Balance == null)
            {
                return;
            }

            var tuning = GameRuntime.Balance.monster;
            var target = patrolPoints[patrolIndex % patrolPoints.Count];
            var delta = target.position - transform.position;
            delta.y = 0f;
            transform.position += delta.normalized * speed * tuning.patrolSpeedMultiplier * Time.deltaTime;
            if (delta.magnitude < tuning.patrolArrivalDistance)
            {
                patrolIndex++;
            }
        }

        private void ApplyVisuals()
        {
            var activeDefinitionIndex = GetActiveDefinitionIndex();
            if (meshRenderer != null && contentDatabase != null && activeDefinitionIndex >= 0)
            {
                meshRenderer.sharedMaterial.color = contentDatabase.monsterPool[activeDefinitionIndex].tint;
            }
        }

        private int GetActiveDefinitionIndex()
        {
            var index = definitionIndex.Value >= 0 ? definitionIndex.Value : localDefinitionIndex;
            return contentDatabase != null && index >= 0 && index < contentDatabase.monsterPool.Count ? index : -1;
        }
    }
}
