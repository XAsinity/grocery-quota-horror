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

        private RunConfig runConfig;
        private List<Transform> patrolPoints;
        private int patrolIndex;
        private float attackCooldown;

        public void Initialize(int configIndex, RunConfig config, IReadOnlyList<Transform> scenePatrolPoints)
        {
            definitionIndex.Value = configIndex;
            runConfig = config;
            patrolPoints = new List<Transform>(scenePatrolPoints);
        }

        public override void OnNetworkSpawn()
        {
            if (runConfig == null && NightGameManager.Instance != null)
            {
                runConfig = NightGameManager.Instance.RunConfig;
            }

            ApplyVisuals();
        }

        private void Update()
        {
            if (!IsServer || runConfig == null || definitionIndex.Value < 0)
            {
                return;
            }

            attackCooldown -= Time.deltaTime;
            var definition = runConfig.monsterPool[definitionIndex.Value];
            var target = FindTarget(definition);
            if (target == null)
            {
                Patrol(definition.moveSpeed);
                return;
            }

            var flat = target.transform.position - transform.position;
            flat.y = 0f;
            transform.position += flat.normalized * definition.moveSpeed * Time.deltaTime;
            transform.forward = Vector3.Lerp(transform.forward, flat.normalized, 0.12f);

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
            if (patrolPoints == null || patrolPoints.Count == 0)
            {
                return;
            }

            var target = patrolPoints[patrolIndex % patrolPoints.Count];
            var delta = target.position - transform.position;
            delta.y = 0f;
            transform.position += delta.normalized * speed * 0.55f * Time.deltaTime;
            if (delta.magnitude < 1.2f)
            {
                patrolIndex++;
            }
        }

        private void ApplyVisuals()
        {
            if (meshRenderer != null && runConfig != null && definitionIndex.Value >= 0)
            {
                meshRenderer.sharedMaterial.color = runConfig.monsterPool[definitionIndex.Value].tint;
            }
        }
    }
}
