using UnityEngine;

/// <summary>
/// 로딩 화면에 표시되는 스피너 이미지를 계속 회전시키는 스크립트.
/// 이 오브젝트가 LoadingPanel의 자식이라면, 로딩 화면이 꺼질 때(부모 비활성화)
/// 자동으로 같이 멈추니 별도 처리가 필요 없습니다.
/// </summary>
public class LoadingSpinner : MonoBehaviour
{
    [Tooltip("초당 회전 속도(도). 양수면 시계 방향, 음수면 반시계 방향")]
    public float rotationSpeedDegreesPerSecond = -180f;

    void Update()
    {
        transform.Rotate(0f, 0f, rotationSpeedDegreesPerSecond * Time.deltaTime);
    }
}
