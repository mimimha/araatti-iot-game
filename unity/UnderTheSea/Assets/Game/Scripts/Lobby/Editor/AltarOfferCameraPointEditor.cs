using UnderTheSea.Lobby;
using UnityEditor;
using UnityEngine;

namespace Lobby.Editor
{
    /// <summary>
    /// 제단 봉헌 카메라 자리(<see cref="AltarOfferCameraPoint"/>)를 씬 뷰로 고르는 도구.
    ///
    /// <code>
    ///   [이 시점으로 씬 뷰 보기]      씬 뷰를 지금 저장된 자리로 옮긴다 — 연출 화면을 미리 본다
    ///   [지금 씬 뷰 시점을 저장]      씬 뷰 카메라 자리를 이 오브젝트에 옮기고 프리팹에 바로 저장한다
    /// </code>
    ///
    /// ⚠ <b>로비 씬에 흔적을 남기지 않는다.</b> 로비의 제단은 <c>P_HeartAltar</c> 프리팹의 인스턴스라, 씬에서 옮기면
    ///    씬 파일에 덮어쓰기(override)가 쌓인다. 저장 버튼은 옮긴 값을 프리팹으로 올리고(Apply) 씬 쪽 덮어쓰기를 지운다.
    ///    그래도 로비 씬 자체는 저장하지 않아도 된다 — 바뀐 것은 프리팹 파일 하나다.
    /// </summary>
    [CustomEditor(typeof(AltarOfferCameraPoint))]
    public sealed class AltarOfferCameraPointEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var point = (AltarOfferCameraPoint)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "씬 뷰를 원하는 각도로 맞춘 뒤 [지금 씬 뷰 시점을 저장]을 누르세요. 위치 · 방향 · 화각이 제단 프리팹에 바로 저장됩니다.\n" +
                "줌을 바꾸려면 씬 뷰 카메라 설정(Scene 창 오른쪽 위 카메라 아이콘)의 Field of View 를 바꾼 뒤 저장하세요.\n" +
                "빌드한 게임에서는 로비 개발자 패널(P)의 \\ 키로 연출을 확인합니다.",
                MessageType.Info);

            if (GUILayout.Button("이 시점으로 씬 뷰 보기", GUILayout.Height(28)))
            {
                ViewFrom(point);
            }

            if (GUILayout.Button("지금 씬 뷰 시점을 저장", GUILayout.Height(28)))
            {
                SaveFromSceneView(point);
            }
        }

        [MenuItem("Tools/아라아띠/제단 봉헌 카메라 고르기")]
        public static void Pick()
        {
            var point = Object.FindFirstObjectByType<AltarOfferCameraPoint>(FindObjectsInactive.Include);
            if (point == null)
            {
                EditorUtility.DisplayDialog("제단 봉헌 카메라",
                    "열린 씬에 제단 카메라 자리가 없습니다.\n\n" +
                    "로비 씬(Lobby.unity)을 열고 다시 눌러 주세요. 그래도 없으면 " +
                    "Tools/아라아띠/제단 봉헌 빛줄기 설치 를 먼저 누르세요.", "확인");
                return;
            }

            Selection.activeGameObject = point.gameObject;
            ViewFrom(point);
        }

        private static void ViewFrom(AltarOfferCameraPoint point)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                Debug.LogWarning("[제단 봉헌 카메라] 열린 씬 뷰가 없습니다. Scene 창을 한 번 열어 주세요.");
                return;
            }

            view.cameraSettings.fieldOfView = point.FieldOfView;
            view.orthographic = false;
            view.AlignViewToObject(point.transform);
            view.Repaint();
        }

        private static void SaveFromSceneView(AltarOfferCameraPoint point)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
            {
                Debug.LogWarning("[제단 봉헌 카메라] 열린 씬 뷰가 없습니다.");
                return;
            }

            Transform t = point.transform;
            Transform cam = view.camera.transform;

            Undo.RecordObject(t, "제단 봉헌 카메라 자리 저장");
            t.SetPositionAndRotation(cam.position, cam.rotation);

            // 화각도 같이 — 게임은 연출하는 동안 이 화각을 쓴다. 안 맞추면 씬 뷰와 확대 정도가 달라진다.
            var so = new SerializedObject(point);
            so.FindProperty("fieldOfView").floatValue = view.cameraSettings.fieldOfView;
            so.ApplyModifiedProperties();

            // 씬의 프리팹 인스턴스면 프리팹으로 올린다. 프리팹 편집 화면이면 그 화면이 알아서 저장한다.
            if (PrefabUtility.IsPartOfPrefabInstance(t))
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t);
                PrefabUtility.ApplyObjectOverride(t, path, InteractionMode.UserAction);
                PrefabUtility.ApplyObjectOverride(point, path, InteractionMode.UserAction);
                AssetDatabase.SaveAssets();
                Debug.Log($"[제단 봉헌 카메라] 저장했습니다 — {path}  위치 {t.position.ToString("F1")} · 각도 {t.eulerAngles.ToString("F0")} · 화각 {point.FieldOfView:0}°");
            }
            else
            {
                EditorUtility.SetDirty(t);
                Debug.Log($"[제단 봉헌 카메라] 옮겼습니다 — 위치 {t.position.ToString("F1")} · 각도 {t.eulerAngles.ToString("F0")}. " +
                          "프리팹 편집 화면이면 저장(Ctrl+S)하세요.");
            }
        }
    }
}
