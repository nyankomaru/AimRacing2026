//F9とMT開始キーをEditorと共通の自動走行へ接続するクラス
public static class AutoDemoDriver
{
    //既存のF9呼び出しをTrack ATへ接続する関数
    public static void Start() { Start(false); }

    //選択された変速方式で最新の共通走行処理を開始する関数
    public static void Start(bool manual) { TrackDriveSession.BeginRuntime(manual); }

    //ビルド中だけ共通走行処理の更新を進める関数
    public static void Tick() { TrackDriveSession.RuntimeTick(); }
}
