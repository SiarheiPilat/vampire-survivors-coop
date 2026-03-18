using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using VampireSurvivors.Components;

namespace VampireSurvivors.MonoBehaviours
{
    /// <summary>
    /// Attach to Main Camera. Each LateUpdate, queries all player positions from ECS,
    /// lerps the camera to their centroid, and adjusts orthographic size based on spread.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] float followSpeed = 5f;
        [SerializeField] float baseSize    = 5f;
        [SerializeField] float zoomFactor  = 0.4f;
        [SerializeField] float maxSize     = 12f;

        Camera      _cam;
        EntityQuery _playerQuery;
        bool        _queryCreated;

        void Start()
        {
            _cam = GetComponent<Camera>();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            _playerQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            _queryCreated = true;
        }

        void OnDisable()
        {
            if (_queryCreated)
            {
                _playerQuery.Dispose();
                _queryCreated = false;
            }
        }

        void LateUpdate()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !_queryCreated) return;

            var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (transforms.Length == 0)
            {
                transforms.Dispose();
                return;
            }

            // Centroid
            float3 centroid = float3.zero;
            for (int i = 0; i < transforms.Length; i++)
                centroid += transforms[i].Position;
            centroid /= transforms.Length;

            // Spread — max distance from centroid to any player
            float maxDist = 0f;
            for (int i = 0; i < transforms.Length; i++)
            {
                float d = math.distance(transforms[i].Position.xy, centroid.xy);
                if (d > maxDist) maxDist = d;
            }

            transforms.Dispose(); // MUST dispose before returning

            // Move camera toward centroid
            var targetPos = new Vector3(centroid.x, centroid.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);

            // Zoom out as players spread apart
            float targetSize = math.min(baseSize + maxDist * zoomFactor, maxSize);
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize, followSpeed * Time.deltaTime);
        }
    }
}
