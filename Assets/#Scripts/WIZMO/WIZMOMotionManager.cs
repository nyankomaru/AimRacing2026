using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuMotionManager : MonoBehaviour
{
    [SerializeField] private WIZMOController m_wizmoController;
    [SerializeField] private VehicleController m_vehicleController;

    [Header("Motion Settings")]
    [SerializeField] private float m_menuHeave = 1f;        // メニュー時の目標高さ
    [SerializeField] private float m_motionSpeed = 0.2f;    // WIZMO側の移動速度
    [SerializeField] private float m_motionSmooth = 0.2f;   // WIZMO側の滑らかさ
    [SerializeField] private float m_transitionWait = 2.0f; // インゲーム開始時の復帰待機時間

    // 前回の状態を記憶し、状態が変わった瞬間だけ処理を切り替える
    private bool? m_lastInGameState = null;
    private float m_inGameTimer = 0f;

    public static bool IsMenuTransitioning { get; private set; } = false;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // 起動時に1回だけ現在の状態を確定させる
        UpdateSceneState(forceReset: true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // マネージャーシーン自身が遅延ロードされた時の通知は無視する
        if (scene == gameObject.scene) return;

        // 別のシーンが追加・切り替えされた時だけ状態を再確認
        UpdateSceneState(forceReset: false);
    }

    private void UpdateSceneState(bool forceReset)
    {
        if (m_vehicleController == null)
        {
#if UNITY_2023_1_OR_NEWER
            m_vehicleController = FindFirstObjectByType<VehicleController>();
#else
            m_vehicleController = FindObjectOfType<VehicleController>();
#endif
        }

        bool currentInGame = (m_vehicleController != null);

        // 初回、またはメニュー と インゲームの状態が実際に切り替わった時だけタイマーをリセット
        if (forceReset || m_lastInGameState != currentInGame)
        {
            m_lastInGameState = currentInGame;
            m_inGameTimer = 0f;
            IsMenuTransitioning = true;
        }
    }

    private void FixedUpdate()
    {
        if (m_wizmoController == null || m_lastInGameState == null) return;

        // 念のため、破棄されたVehicleControllerの参照が残っていたら更新
        if (m_lastInGameState == true && m_vehicleController == null)
        {
            UpdateSceneState(forceReset: false);
        }

        if (m_lastInGameState == true)
        {
            // インゲーム時：0fへ戻す
            if (IsMenuTransitioning)
            {
                m_wizmoController.SetChairMotion(0f, 0f, 0f, 0f, 0f, 0f, m_motionSpeed, m_motionSmooth);

                m_inGameTimer += Time.fixedDeltaTime;
                if (m_inGameTimer >= m_transitionWait)
                {
                    IsMenuTransitioning = false;
                }
            }
        }
        else
        {
            // メニュー時：目標高さへ移動
            IsMenuTransitioning = true;
            m_wizmoController.SetChairMotion(0f, 0f, 0f, m_menuHeave, 0f, 0f, m_motionSpeed, m_motionSmooth);
        }
    }
}