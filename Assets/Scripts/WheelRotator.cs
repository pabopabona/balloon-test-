using UnityEngine;

/// <summary>
/// 부모(발사대)가 좌우로 이동한 거리만큼, 바퀴가 실제로 굴러가는 것처럼 회전시킵니다.
/// 이동 거리 / 바퀴 둘레(2*PI*반지름) 비율로 회전각을 계산합니다.
/// Launcher(또는 그 자식)에 붙여서, 발사대가 움직일 때마다 자연스럽게 같이 굴러가도록 사용하세요.
/// </summary>
public class WheelRotator : MonoBehaviour
{
    [Tooltip("바퀴의 실제 반지름(월드 유닛 기준). 최종 Scale이 적용된 상태의 반지름을 넣어야 정확합니다. " +
             "예: 바퀴 지름이 0.5유닛이면 0.25를 입력")]
    public float wheelRadius = 0.25f;

    [Tooltip("회전 방향이 반대로 보이면 -1로 바꾸세요")]
    public float directionMultiplier = 1f;

    private float lastWorldX;

    void Start()
    {
        lastWorldX = transform.position.x;
    }

    void Update()
    {
        float currentX = transform.position.x;
        float deltaX = currentX - lastWorldX;
        lastWorldX = currentX;

        if (Mathf.Abs(deltaX) < 0.0001f) return;

        // 이동 거리(deltaX)를 바퀴 둘레로 나눠서 회전 비율을 구하고, 각도(도)로 변환
        float circumference = 2f * Mathf.PI * wheelRadius;
        float angleDelta = (deltaX / circumference) * 360f;

        transform.Rotate(0f, 0f, -angleDelta * directionMultiplier);
    }
}
