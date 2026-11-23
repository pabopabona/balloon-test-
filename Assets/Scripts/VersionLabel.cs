using UnityEngine;
using TMPro;

/// <summary>
/// 시작 화면 구석에 앱 버전(예: v1.1.0)을 작게 보여줍니다.
/// 버전 숫자는 Player Settings의 Version 값을 자동으로 읽으므로, 업데이트 때 따로 고칠 필요가 없습니다.
///
/// 버전을 보여줄 TMP Text 오브젝트에 붙이세요.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class VersionLabel : MonoBehaviour
{
    [Tooltip("{0} = 버전 (Player Settings의 Version)")]
    public string format = "v{0}";

    void Awake()
    {
        GetComponent<TMP_Text>().text = string.Format(format, Application.version);
    }
}
