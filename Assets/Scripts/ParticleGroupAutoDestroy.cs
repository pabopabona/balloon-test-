using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부모/자식 구조로 이루어진 파티클 프리팹의 루트에 붙이는 헬퍼 스크립트.
/// 계층 구조 안의 모든 ParticleSystem이 재생을 완전히 마칠 때까지 기다렸다가,
/// 전부 끝난 시점에만 전체(부모+자식)를 한 번에 Destroy합니다.
/// 각 파티클 프리팹 개별로 Duration/Lifetime을 정확히 맞출 필요가 없어서
/// 더 안전합니다. 각 하위 ParticleSystem의 Stop Action은 "None"으로 두세요
/// (이 스크립트가 대신 정리하므로 개별 Stop Action이 필요 없습니다).
/// </summary>
public class ParticleGroupAutoDestroy : MonoBehaviour
{
    private List<ParticleSystem> allSystems;

    void Start()
    {
        allSystems = new List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>());
    }

    void Update()
    {
        if (allSystems == null || allSystems.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        foreach (ParticleSystem ps in allSystems)
        {
            if (ps != null && ps.IsAlive())
            {
                return; // 하나라도 아직 재생 중이면 대기
            }
        }

        Destroy(gameObject);
    }
}