using UnityEngine;
using UnityEditor;

/// <summary>
/// 모든 AnimationCurve 칸 오른쪽에 복사(C) / 붙여넣기(P) 버튼을 붙여주는 드로어.
/// [CustomPropertyDrawer(typeof(AnimationCurve))]로 등록되어 있어서, 프로젝트의 모든 커브 칸에
/// 자동으로 적용됩니다 (EditorGUILayout.PropertyField로 그리는 칸 포함).
///
/// 반드시 "Editor" 폴더 안에 두세요.
/// </summary>
[CustomPropertyDrawer(typeof(AnimationCurve))]
public class AnimationCurvePropertyDrawer : PropertyDrawer
{
    private const int _buttonWidth = 18;

    // 복사해둔 커브. static이라 다른 오브젝트/컴포넌트의 커브 칸에도 붙여넣을 수 있습니다.
    private static Keyframe[] _buffer;
    private static WrapMode _preWrapMode;
    private static WrapMode _postWrapMode;

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        Rect curveRect = new Rect(pos.x, pos.y, pos.width - _buttonWidth * 2 - 2, pos.height);
        Rect copyRect = new Rect(pos.x + pos.width - _buttonWidth * 2, pos.y, _buttonWidth, pos.height);
        Rect pasteRect = new Rect(pos.x + pos.width - _buttonWidth, pos.y, _buttonWidth, pos.height);

        // BeginProperty/EndProperty: 프리팹에서 바뀐 값 굵게 표시, 우클릭 메뉴 등 기본 기능을 살려줍니다.
        label = EditorGUI.BeginProperty(pos, label, prop);

        // 값이 실제로 바뀌었을 때만 저장합니다.
        // (매번 무조건 대입하면 인스펙터를 열어두기만 해도 씬이 "변경됨" 상태가 될 수 있습니다)
        EditorGUI.BeginChangeCheck();
        AnimationCurve edited = EditorGUI.CurveField(curveRect, label, prop.animationCurveValue);
        if (EditorGUI.EndChangeCheck())
        {
            prop.animationCurveValue = edited;
        }

        // 복사
        if (GUI.Button(copyRect, new GUIContent("C", "커브 복사"), EditorStyles.miniButtonLeft))
        {
            AnimationCurve source = prop.animationCurveValue;
            _buffer = source.keys;
            _preWrapMode = source.preWrapMode;
            _postWrapMode = source.postWrapMode;
        }

        // 붙여넣기 (복사해둔 커브가 없으면 버튼 비활성화)
        using (new EditorGUI.DisabledScope(_buffer == null))
        {
            if (GUI.Button(pasteRect, new GUIContent("P", "커브 붙여넣기"), EditorStyles.miniButtonRight))
            {
                AnimationCurve pasted = new AnimationCurve(_buffer)
                {
                    preWrapMode = _preWrapMode,
                    postWrapMode = _postWrapMode
                };
                prop.animationCurveValue = pasted;
            }
        }

        EditorGUI.EndProperty();
    }
}