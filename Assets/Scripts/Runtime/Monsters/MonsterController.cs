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
        private float attackCooldown;

        private bool IsOfflineLocalMode => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkBootstrap.LocalOfflineMode;

        public void Initialize(int configIndex, GameContentDatabase content, IReadOnlyList<Transform> scenePatrolPoints)
        {
            definitionIndex.Value = configIndex;
            contentDatabase = content;
            patrolPoints = new List<Transform>(scenePatrolPoints);
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
            if ((!IsServer && !IsOfflineLocalMode) || contentDatabase == null || definitionIndex.Value < 0 || GameRuntime.Balance == null)
            {
                return;
            }

            attackCooldown -= Time.deltaTime;
            var definition = contentDatabase.monsterPool[definitionIndex.Value];
            var target = FindTarget(definition);
            if (target == null)
            {
                Patrol(definition.moveSpeed);
                return;
            }

            var flat = target.transform.position - transform.position;
            flat.y = 0f;
            transform.position += flat.normalized * definition.moveSpeed * Time.deltaTime;
            transform.forward = Vector3.Lerp(transform.forward, flat.normalized, GameRuntime.Balance.monster.turnSmoothing);

            if (flat.magnitude <= definition.attackRange && attackCooldown <= 0f)
            {
                target.ApplyDamage();
                attackCooldown = definition.attackCooldown;
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

                var distance = Vector3.Distance(transform.position, players[i].transform.position);
                if (distance < bestDistance && distance <= definition.chaseRange)
                {
                    bestDistance = distance;
                    best = players[i];
                }
            }

            return best;
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
            if (meshRenderer != null && contentDatabase != null && definitionIndex.Value >= 0)
            {
                meshRenderer.sharedMaterial.color = contentDatabase.monsterPool[definitionIndex.Value].tint;
            }
        }
    }
}
