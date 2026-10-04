using UnityEditor;
using UnityEngine;

// 이 파일은 반드시 "Editor"라는 이름의 폴더 안에 두세요 (예: Assets/Scripts/Editor/).
// Editor 폴더 밖에 있으면 에디터에서는 동작하지만 안드로이드/윈도우 빌드가 실패합니다.
[CustomEditor(typeof(UITransformGUICtrl))]
public class UITransformGUICtrlInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (targets.Length > 1)
        {
            EditorGUILayout.LabelField("Multiple Selection is not allowed for this components", EditorStyles.boldLabel);
            return;
        }

        serializedObject.Update();

        SerializedProperty valueType = serializedObject.FindProperty("valueType");
        SerializedProperty positionType = serializedObject.FindProperty("positionType");
        SerializedProperty scaleType = serializedObject.FindProperty("scaleType");

        GUILayout.Label("<UI Rect Trans Animation", EditorStyles.boldLabel);
        GUILayout.Space(10.0f);

        GUILayout.Label("--<트랜스폼 할 게임오브젝트 설정 : None 이면 자기자신>--", EditorStyles.textArea);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("obj"), GUIContent.none);
        GUILayout.Space(5.0f);

        Draw("TimeLength", "Time Length");
        GUILayout.Space(5.0f);
        Draw("Once", "Only Once");
        GUILayout.Space(5.0f);
        Draw("IsManual", "Is manual Play");
        GUILayout.Space(5.0f);
        Draw("UseUnscaledTime", "Use Unscaled Time");
        GUILayout.Space(5.0f);
        Draw("ActivateTargetWhilePlaying", "Activate Target While Playing");
        GUILayout.Space(5.0f);

        EditorGUILayout.PropertyField(valueType, new GUIContent("ValueType"));

        var type = (UITransformGUICtrl.ValueType)valueType.enumValueIndex;

        if (type == UITransformGUICtrl.ValueType.Scale_Value)
        {
            EditorGUILayout.PropertyField(scaleType, new GUIContent("Scale Type"));

            if ((UITransformGUICtrl.ScaleType)scaleType.enumValueIndex == UITransformGUICtrl.ScaleType.One_Value)
            {
                Draw("FAnimation", "Scale Fcurve");
            }
            else
            {
                Draw("FAnimation", "Scale Xcurve");
                Draw("F2Animation", "Scale Ycurve");
            }

            EditorGUILayout.HelpBox("커브 값은 원래 크기에 곱해집니다 (1 = 원래 크기, 0 = 안 보임).", MessageType.None);
        }
        else if (type == UITransformGUICtrl.ValueType.Position_Value)
        {
            EditorGUILayout.PropertyField(positionType, new GUIContent("Position Type"));
            var posType = (UITransformGUICtrl.PositionType)positionType.enumValueIndex;

            if (posType == UITransformGUICtrl.PositionType.AddXY_Value || posType == UITransformGUICtrl.PositionType.NoneXY_Value)
            {
                GUILayout.Label("--<포지션이 none일 경우 커브값만으로 움직임>--", EditorStyles.textArea);
                Draw("XAnimation", "X Anchor Fcurve");
                Draw("YAnimation", "Y Anchor Fcurve");
            }
            else if (posType == UITransformGUICtrl.PositionType.OnlyX_Value)
            {
                Draw("XAnimation", "X Anchor Fcurve");
            }
            else if (posType == UITransformGUICtrl.PositionType.OnlyY_Value)
            {
                Draw("YAnimation", "Y Anchor Fcurve");
            }
            else
            {
                GUILayout.Label("--<포지션타입이 Target이므로 타겟을 반드시 설정 후 커브값으로 컨트롤>--", EditorStyles.textArea);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetObj"), GUIContent.none);
                Draw("TargetAnimation", "Target Fcurve");
            }
        }
        else
        {
            Draw("RAnimation", "Z Rotation Fcurve");
        }

        // 변경 사항 적용 (Undo, 프리팹 오버라이드 표시까지 자동으로 처리됩니다)
        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName, string displayName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"'{propertyName}' 필드를 찾을 수 없습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(displayName));
    }
}