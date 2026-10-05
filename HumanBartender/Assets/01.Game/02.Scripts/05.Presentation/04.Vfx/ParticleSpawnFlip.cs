using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleSpawnFlip : MonoBehaviour
{
    private ParticleSystem ps;
    private ParticleSystem.Particle[] particles;
    private Dictionary<uint, float> prevXPositions = new Dictionary<uint, float>();
    private HashSet<uint> aliveSeeds = new HashSet<uint>();
    private List<uint> keysToRemove = new List<uint>();

    [Header("플립 설정")]
    [Tooltip("노이즈의 미세 진동을 무시하기 위한 최소 이동 거리 (기본 0.05)")]
    public float flipThreshold = 0.05f;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
    }

    void LateUpdate()
    {
        if (particles == null || particles.Length < ps.main.maxParticles)
        {
            particles = new ParticleSystem.Particle[ps.main.maxParticles];
        }

        int numAlive = ps.GetParticles(particles);
        aliveSeeds.Clear();

        for (int i = 0; i < numAlive; i++)
        {
            uint seed = particles[i].randomSeed;
            aliveSeeds.Add(seed);

            float currentX = particles[i].position.x;

            if (prevXPositions.ContainsKey(seed))
            {
                float prevX = prevXPositions[seed];
                float deltaX = currentX - prevX;

                // 노이즈 덜덜거림을 무시하고, 확실히 이동했을 때만 방향 전환
                if (Mathf.Abs(deltaX) > flipThreshold)
                {
                    if (deltaX < 0)
                    {
                        // 왼쪽으로 이동 -> Y축 180도 회전 (텍스쳐 좌우 플립)
                        particles[i].rotation3D = new Vector3(0, 180, 0);
                    }
                    else if (deltaX > 0)
                    {
                        // 오른쪽으로 이동 -> 원본 방향 (0도)
                        particles[i].rotation3D = Vector3.zero;
                    }

                    // 방향이 바뀔 만큼 유의미하게 이동했을 때만 이전 위치 저장
                    prevXPositions[seed] = currentX;
                }
            }
            else
            {
                // 처음 태어난 파리는 기본 0도
                particles[i].rotation3D = Vector3.zero;
                prevXPositions[seed] = currentX;
            }
        }

        // 바뀐 회전값을 최종 적용
        ps.SetParticles(particles, numAlive);

        // 죽은 파티클 메모리 정리 (최적화)
        if (prevXPositions.Count > numAlive)
        {
            keysToRemove.Clear();
            foreach (var key in prevXPositions.Keys)
            {
                if (!aliveSeeds.Contains(key)) keysToRemove.Add(key);
            }
            foreach (var key in keysToRemove)
            {
                prevXPositions.Remove(key);
            }
        }
    }
}