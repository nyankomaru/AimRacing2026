/*━━━━━━━━━
@file    GameFlowRunner.cs
@brief   State遷移とシーン読込/解放を統括するイベント駆動の遷移エンジン
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/17
━━━━━━━━━*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowRunner : SingletonMonoBehaviour<GameFlowRunner>
{
    private const string k_LoadingSceneName = "Loading"; //遷移の裏で一時的に追加ロードするLoading専用シーン名
    private const int k_StableFrameCount = 2; //Enter()後、レンダリング安定のためLoading画面を維持したまま待つフレーム数

    /// <summary>
    /// 指定StateへのFadeInが完了し、画面が完全に見えるようになった直後に発火します。
    /// Enter()の時点ではまだLoading画面やFadeで隠れている場合があるため、
    /// 「確実に描画されてから」何かを始めたいUI側はこちらを購読してください。
    /// </summary>
    public static event Action<GameStateId> OnStateFadeInComplete;

    //Loading画面を挟む離脱元State一覧(Intro→RacingはシーングループがMapで一致し差分が無いため対象外)
    private static readonly HashSet<GameStateId> k_LoadingScreenLeavingStates = new HashSet<GameStateId>
    {
        GameStateId.Splash, GameStateId.Opening, GameStateId.Menu, GameStateId.Racing, GameStateId.Result
    };

    [SerializeField] private bool m_useLoadingScreen = true; //falseにすると全遷移が従来のフェードのみに戻る

    private Dictionary<GameStateId, IGameState> m_states = new Dictionary<GameStateId, IGameState>(); //自己登録されたState実装
    private GameStateId m_currentId; //現在のState
    private bool m_isTransitioning; //遷移処理中は多重遷移を禁止するフラグ

    public GameStateId CurrentId => m_currentId;

    /// <summary>
    /// Stateを自己登録します。GameStateBindingBase.Awakeから呼ばれます。
    /// </summary>
    /// <param name="_state">登録するState実装</param>
    public void Register(IGameState _state)
    {
        if (_state == null) { return; }
        m_states[_state.Id] = _state;
    }

    /// <summary>
    /// 登録済みのStateを解除します。シーンアンロード時のクリーンアップ用です。
    /// </summary>
    /// <param name="_state">解除するState実装</param>
    public void Unregister(IGameState _state)
    {
        if (_state == null) { return; }
        if (!m_states.TryGetValue(_state.Id, out IGameState registered)) { return; }
        if (registered != _state) { return; } //既に別インスタンスへ差し替わっている場合は無視する
        m_states.Remove(_state.Id);
    }

    private void Update()
    {
        if (m_isTransitioning) { return; } //遷移中は現Stateへのティックを止める
        if (m_states.TryGetValue(m_currentId, out IGameState current))
        {
            current.Tick(Time.deltaTime);
        }
    }

    /// <summary>
    /// 初回起動時、遷移ではなく直接指定Stateへ入ります。Managerシーンのロード完了直後に[FlowBinding]自身から一度だけ呼ばれます。
    /// </summary>
    /// <param name="_initialState">最初に入るState</param>
    public void Bootstrap(IGameState _initialState)
    {
        if (_initialState == null) { return; }
        StartCoroutine(BootstrapRoutine(_initialState));
    }

    /// <summary>
    /// 初期StateのシーングループのうちManager以外でまだ読み込まれていないシーン(MapEnvironment等)を
    /// 起動時にまとめて読み込んでから、指定Stateへ入ります。
    /// </summary>
    /// <param name="_initialState">最初に入るState</param>
    private IEnumerator BootstrapRoutine(IGameState _initialState)
    {
        Register(_initialState);
        m_currentId = _initialState.Id;

        GameSceneGroup initialGroup = GameFlowGraph.SceneGroups[_initialState.Id];
        foreach (string sceneName in initialGroup.SceneNames) //MapEnvironment等、未読込のシーンを起動時にまとめて読み込む
        {
            if (IsSceneLoaded(sceneName)) { continue; }
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        //ロード直後の数フレームはレンダリングが安定していない可能性があるため、
        //黒画面(TransitionFadeControllerの起動時初期値)で覆ったまま安定するのを待ってからSplashを表示する
        for (int i = 0; i < k_StableFrameCount; ++i)
        {
            yield return null;
        }

        EnsureTitleLogoHiddenFor(_initialState.Id); //Opening以外の初期StateでロゴがEnter前から表示されていないことを保証する
        _initialState.Enter();
        AppLog.Log("[LoadTiming] Bootstrap完了(起動〜操作可能まで): " + Time.realtimeSinceStartup.ToString("F2") + "s");
        StartCoroutine(BootstrapFadeInRoutine(_initialState.Id)); //起動直後の黒画面から最初のFadeIn
    }

    /// <summary>
    /// Bootstrap時のFadeInを実行し、完了後にOnStateFadeInCompleteを発火します。
    /// </summary>
    /// <param name="_stateId">FadeIn完了を通知するState</param>
    private IEnumerator BootstrapFadeInRoutine(GameStateId _stateId)
    {
        yield return TransitionFadeController.Instance.FadeIn(ResolveFadeInDuration());
        OnStateFadeInComplete?.Invoke(_stateId);
    }

    /// <summary>
    /// GameFlowGraph.NextStateに従って次のStateへ遷移します。各Stateは自分の次を知る必要がありません。
    /// </summary>
    public void AdvanceToNext()
    {
        if (!GameFlowGraph.NextState.TryGetValue(m_currentId, out GameStateId nextId))
        {
            AppLog.LogWarning("[GameFlowRunner] " + m_currentId + " の次のStateがGameFlowGraphに定義されていません。");
            return;
        }
        TransitionTo(nextId);
    }

    /// <summary>
    /// 全シーン(Manager以外、MapEnvironment含む)を破棄し、指定Stateへ入り直す緊急リセットです。通常フローでは使いません。
    /// </summary>
    /// <param name="_targetId">リセット後に入るState</param>
    public void EmergencyReset(GameStateId _targetId)
    {
        if (m_isTransitioning) { return; }
        StartCoroutine(EmergencyResetRoutine(_targetId));
    }

    private IEnumerator EmergencyResetRoutine(GameStateId _targetId)
    {
        AppLog.Log("[SceneFlow] 緊急リセット開始: " + m_currentId + " → " + _targetId);
        float logStart = Time.realtimeSinceStartup; //[LoadTiming] 計測用の基準時刻
        float logPrev = logStart;
        m_isTransitioning = true;
        yield return TransitionFadeController.Instance.FadeOut(ResolveFadeOutDuration(), ResolveFadeOutColor()); //画面の裏で全シーン破棄・再構築を行う
        logPrev = LogTimingStep("FadeOut", logPrev);

        m_states.Clear();

        Scene managerScene = gameObject.scene; //自分自身が常駐するシーン
        for (int i = SceneManager.sceneCount - 1; i >= 0; --i) //後ろから走査してアンロードによるインデックスずれを避ける
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene == managerScene) { continue; }
            yield return SceneManager.UnloadSceneAsync(scene);
            logPrev = LogTimingStep("Unload " + scene.name, logPrev);
        }

        GameSceneGroup targetGroup = GameFlowGraph.SceneGroups[_targetId];
        foreach (string sceneName in targetGroup.SceneNames)
        {
            if (sceneName == managerScene.name) { continue; }
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            logPrev = LogTimingStep("Load " + sceneName, logPrev);
        }

        SceneManager.SetActiveScene(SceneManager.GetSceneByName(targetGroup.PrimarySceneName));
        m_currentId = _targetId;
        m_isTransitioning = false;
        EnsureTitleLogoHiddenFor(_targetId); //EmergencyResetはLoadingScreenControllerを経由しないため、ここが唯一のロゴ強制非表示のタイミング

        if (m_states.TryGetValue(_targetId, out IGameState state))
        {
            state.Enter();
        }
        logPrev = LogTimingStep("Enter", logPrev);

        yield return TransitionFadeController.Instance.FadeIn(ResolveFadeInDuration());
        LogTimingStep("FadeIn", logPrev);
        OnStateFadeInComplete?.Invoke(_targetId);
        AppLog.Log("[LoadTiming] EmergencyReset(" + _targetId + ") 合計: " + (Time.realtimeSinceStartup - logStart).ToString("F2") + "s");
    }

    private void TransitionTo(GameStateId _nextId)
    {
        if (m_isTransitioning) { return; }
        StartCoroutine(TransitionRoutine(_nextId));
    }

    private IEnumerator TransitionRoutine(GameStateId _nextId)
    {
        AppLog.Log("[SceneFlow] 遷移開始: " + m_currentId + " → " + _nextId);
        float logStart = Time.realtimeSinceStartup; //[LoadTiming] 計測用の基準時刻
        float logPrev = logStart;
        m_isTransitioning = true;
        yield return TransitionFadeController.Instance.FadeOut(ResolveFadeOutDuration(), ResolveFadeOutColor()); //画面の裏でシーン切替を行う
        logPrev = LogTimingStep("FadeOut", logPrev);

        bool useLoadingScreen = ShouldUseLoadingScreen(m_currentId); //この遷移でLoading画面を挟むか
        AppLog.Log("[SceneFlow] Loading画面使用: " + useLoadingScreen);
        LoadingScreenController loadingController = null;
        if (useLoadingScreen)
        {
            yield return SceneManager.LoadSceneAsync(k_LoadingSceneName, LoadSceneMode.Additive);
            logPrev = LogTimingStep("Load " + k_LoadingSceneName, logPrev);

            loadingController = FindLoadingScreenController(SceneManager.GetSceneByName(k_LoadingSceneName));
            if (loadingController != null)
            {
                loadingController.Configure(m_currentId, _nextId);
                yield return loadingController.PlayFadeIn(); //FadeIn完了まで次シーンのロード開始を遅らせる
                logPrev = LogTimingStep("LoadingFadeIn", logPrev);
            }
        }

        if (m_states.TryGetValue(m_currentId, out IGameState currentState))
        {
            currentState.Exit();
        }

        GameSceneGroup currentGroup = GameFlowGraph.SceneGroups[m_currentId];
        GameSceneGroup nextGroup = GameFlowGraph.SceneGroups[_nextId];

        List<string> scenesToLoad = new List<string>(); //次グループにのみ存在するシーン
        foreach (string sceneName in nextGroup.SceneNames)
        {
            if (IsSceneLoaded(sceneName)) { continue; }
            scenesToLoad.Add(sceneName);
        }

        List<string> scenesToUnload = new List<string>(); //現グループにのみ存在するシーン
        foreach (string sceneName in currentGroup.SceneNames)
        {
            if (Contains(nextGroup.SceneNames, sceneName)) { continue; }
            scenesToUnload.Add(sceneName);
        }

        for (int i = 0; i < scenesToLoad.Count; ++i)
        {
            yield return SceneManager.LoadSceneAsync(scenesToLoad[i], LoadSceneMode.Additive);
            logPrev = LogTimingStep("Load " + scenesToLoad[i], logPrev);
        }

        for (int i = 0; i < scenesToUnload.Count; ++i)
        {
            yield return SceneManager.UnloadSceneAsync(scenesToUnload[i]);
            logPrev = LogTimingStep("Unload " + scenesToUnload[i], logPrev);
        }

        if (useLoadingScreen && loadingController != null)
        {
            loadingController.TriggerOpeningTitleReveal(); //Splash→Opening限定。Splashが実際に消えた直後・SetActiveScene前にタイトルロゴFadeIn/BGM先行再生を開始する
        }

        SceneManager.SetActiveScene(SceneManager.GetSceneByName(nextGroup.PrimarySceneName));
        m_currentId = _nextId;
        m_isTransitioning = false;
        EnsureTitleLogoHiddenFor(_nextId); //Menuはここでは対象外(この後のPlayFadeOut()が正規のFadeOutタイミングで隠す)

        if (m_states.TryGetValue(_nextId, out IGameState nextState))
        {
            nextState.Enter();
        }
        logPrev = LogTimingStep("Enter", logPrev);

        if (useLoadingScreen)
        {
            //Enter()直後の数フレームはレンダリングが安定していない可能性があるため、
            //Loading画面で覆ったまま安定するのを待ってから隠す
            for (int i = 0; i < k_StableFrameCount; ++i)
            {
                yield return null;
            }

            if (loadingController != null)
            {
                yield return loadingController.WaitForKeep(); //離脱元State別の保持秒数だけ待機(Splash→Openingは3秒、他は0秒)
                yield return loadingController.PlayFadeOut(); //Loading画面をFadeOutで隠す
                logPrev = LogTimingStep("LoadingFadeOut", logPrev);
            }

            yield return SceneManager.UnloadSceneAsync(k_LoadingSceneName);
            logPrev = LogTimingStep("Unload " + k_LoadingSceneName, logPrev);
        }

        int gcCountBeforeFadeIn = System.GC.CollectionCount(0); //[LoadTiming] GC発生有無の切り分け用
        yield return TransitionFadeController.Instance.FadeIn(ResolveFadeInDuration());
        int gcCountAfterFadeIn = System.GC.CollectionCount(0);
        LogTimingStep("FadeIn", logPrev);
        if (gcCountAfterFadeIn != gcCountBeforeFadeIn)
        {
            AppLog.Log("[LoadTiming]   ※FadeIn中にGC発生(Gen0: " + gcCountBeforeFadeIn + "→" + gcCountAfterFadeIn + "回)");
        }
        OnStateFadeInComplete?.Invoke(_nextId);
        AppLog.Log("[LoadTiming] " + _nextId + "への遷移 合計: " + (Time.realtimeSinceStartup - logStart).ToString("F2") + "s");
    }

    /// <summary>
    /// 直前の計測ポイントからの経過時間をログ出力します。
    /// </summary>
    /// <param name="_label">ログに表示するステップ名</param>
    /// <param name="_prevTime">直前の計測時刻(Time.realtimeSinceStartup)</param>
    private float LogTimingStep(string _label, float _prevTime)
    {
        float now = Time.realtimeSinceStartup;
        AppLog.Log("[LoadTiming]   " + _label + ": " + (now - _prevTime).ToString("F2") + "s");
        return now;
    }

    //現在離れようとしているStateに応じたフェード色を取得する関数(TransitionFadeController側のInspector設定を参照)
    private Color ResolveFadeOutColor()
    {
        return TransitionFadeController.Instance.ResolveFadeOutColor(m_currentId);
    }

    //現在離れようとしているStateに応じたFadeOut秒数を取得する関数(未登録ならnull=フェード無し)
    private float? ResolveFadeOutDuration()
    {
        return TransitionFadeController.Instance.ResolveFadeOutDuration(m_currentId);
    }

    //現在のStateに応じたFadeIn秒数を取得する関数(未登録ならnull=フェード無し)
    private float? ResolveFadeInDuration()
    {
        return TransitionFadeController.Instance.ResolveFadeInDuration(m_currentId);
    }

    private bool IsSceneLoaded(string _sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(_sceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    private bool Contains(IReadOnlyList<string> _list, string _value)
    {
        for (int i = 0; i < _list.Count; ++i)
        {
            if (_list[i] == _value) { return true; }
        }
        return false;
    }

    //離脱元Stateに応じて、この遷移でLoading画面を挟むべきかを判定する関数
    private bool ShouldUseLoadingScreen(GameStateId _leavingState)
    {
        return m_useLoadingScreen && k_LoadingScreenLeavingStates.Contains(_leavingState);
    }

    /// <summary>
    /// Opening/Menu以外のStateへ入る直前に呼び、TitleLogoOverlayを強制的に非表示化します。
    /// LoadingScreenController経由のFadeIn/FadeOutを通らない遷移(EmergencyReset等)でも、
    /// タイトルロゴがSplash/Map/Result等に残ってしまわないことを保証する安全網です。
    /// </summary>
    /// <param name="_enteringState">これから入るState</param>
    private void EnsureTitleLogoHiddenFor(GameStateId _enteringState)
    {
        //Opening=表示中、Menu=LoadingScreenController.PlayFadeOut()が正規のFadeOutタイミングで隠すため、
        //ここで先回りして強制非表示にしない(EmergencyReset(F1/F2)の対象はSplash/Racingのみで
        //Menuを直接リセットすることは無いため、対象外にしても安全網としての効果は保たれる)
        if (_enteringState == GameStateId.Opening || _enteringState == GameStateId.Menu) { return; }
        if (TitleLogoOverlay.Instance != null) { TitleLogoOverlay.Instance.ForceHide(); }
    }

    /// <summary>
    /// 追加ロードしたLoadingシーンのルートオブジェクトからLoadingScreenControllerを検索します。
    /// </summary>
    /// <param name="_scene">検索対象のLoadingシーン</param>
    private LoadingScreenController FindLoadingScreenController(Scene _scene)
    {
        foreach (GameObject rootObject in _scene.GetRootGameObjects())
        {
            LoadingScreenController controller = rootObject.GetComponentInChildren<LoadingScreenController>(true);
            if (controller != null)
            {
                return controller;
            }
        }
        return null;
    }
}
