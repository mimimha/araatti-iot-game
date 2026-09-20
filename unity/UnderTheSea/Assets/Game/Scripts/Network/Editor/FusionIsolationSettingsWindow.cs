using UnityEditor;
using UnityEngine;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// 에디터에서 <b>Photon 지역과 AppVersion 을 정해 두는</b> 작은 창.
    ///
    /// 빌드한 실행 파일은 <c>-region</c> · <c>-appver</c> 인자로 이 값을 받지만, Unity Hub 로
    /// 여는 에디터에는 인자를 붙일 수 없다. 에디터에서 클라이언트를 돌리려면
    /// (Multiplayer Play Mode) 다른 입구가 필요해서 이 창을 만들었다.
    ///
    /// <b>지역을 비워 두면 에디터는 접속하지 못한다.</b> Photon 은 접속할 때마다 지역 14곳에
    /// 핑을 쏘는데 에디터는 무거워서 제한 시간을 넘긴다. 자세한 것은
    /// <see cref="FusionSessionIsolation"/> 의 주석에 적어 두었다.
    ///
    /// 값은 <c>EditorPrefs</c> 에 들어가므로 <b>이 PC 에만</b> 남는다. 저장소에 들어가지 않아
    /// 팀원에게 퍼지지 않고, 브랜치를 옮겨도 그대로다.
    /// </summary>
    public class FusionIsolationSettingsWindow : EditorWindow
    {
        /// <summary>한국 지역 코드. 대부분 이 값이면 된다.</summary>
        private const string DefaultRegion = "kr";

        private string _region;
        private string _appVersion;

        [MenuItem("Tools/아라아띠/Photon 격리 설정")]
        public static void Open()
        {
            FusionIsolationSettingsWindow window = GetWindow<FusionIsolationSettingsWindow>(true, "Photon 격리 설정");
            window.minSize = new Vector2(460f, 260f);
        }

        private void OnEnable()
        {
            _region = EditorPrefs.GetString(FusionSessionIsolation.EditorRegionPref, string.Empty);
            _appVersion = EditorPrefs.GetString(FusionSessionIsolation.EditorAppVersionPref, string.Empty);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "여기 넣은 값은 이 PC 의 에디터에만 적용됩니다. 저장소에 들어가지 않으니 " +
                "팀원에게 퍼지지 않습니다.\n\n" +
                "빌드한 실행 파일은 지금처럼 -region · -appver 인자를 씁니다. " +
                "인자가 있으면 그쪽이 이깁니다.",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("지역 (Region)", EditorStyles.boldLabel);
            _region = EditorGUILayout.TextField("지역 코드", _region);
            EditorGUILayout.LabelField(
                "비워 두면 에디터는 접속하지 못합니다. 지역 핑에 시간을 다 써서 시간 초과가 납니다.",
                EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button($"한국({DefaultRegion}) 으로 맞추기"))
            {
                _region = DefaultRegion;
                GUI.FocusControl(null);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("AppVersion", EditorStyles.boldLabel);
            _appVersion = EditorGUILayout.TextField("AppVersion", _appVersion);
            EditorGUILayout.LabelField(
                "같은 값을 넣은 사람끼리만 만납니다. 비워 두면 팀 공용이라 " +
                "세션 이름이 겹칠 때 남의 서버에 붙을 수 있습니다. " +
                "DS 에는 -appver 로 같은 값을 주세요.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space();

            if (GUILayout.Button("저장", GUILayout.Height(30f)))
            {
                Save();
            }

            EditorGUILayout.Space();

            // ⚠ 다음 Play 부터 적용된다. FusionSessionIsolation 은 값을 한 번만 읽고 기억한다.
            EditorGUILayout.HelpBox(
                "저장한 값은 다음 Play 부터 적용됩니다. 지금 플레이 중이라면 한 번 멈췄다 다시 눌러 주세요.",
                MessageType.None);
        }

        private void Save()
        {
            string region = (_region ?? string.Empty).Trim();
            string version = (_appVersion ?? string.Empty).Trim();

            EditorPrefs.SetString(FusionSessionIsolation.EditorRegionPref, region);
            EditorPrefs.SetString(FusionSessionIsolation.EditorAppVersionPref, version);

            _region = region;
            _appVersion = version;

            Debug.Log(
                "[Photon 격리 설정] 저장했습니다 — " +
                $"지역 \"{(region.Length == 0 ? "(비어 있음)" : region)}\", " +
                $"AppVersion \"{(version.Length == 0 ? "(비어 있음)" : version)}\". " +
                "다음 Play 부터 적용됩니다.");
        }
    }
}
