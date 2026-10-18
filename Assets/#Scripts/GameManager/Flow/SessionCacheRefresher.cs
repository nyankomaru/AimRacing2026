/*━━━━━━━━━
@file    SessionCacheRefresher.cs
@brief   1セッション分のプレイで溜まる状態・キャッシュをリフレッシュする共通処理
         (元はLoadingScreenController.RefreshSessionCachesに実装されていたものをLoadStateと共用するために抜き出した)
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/16
         最終更新日 : 2026/09/18
━━━━━━━━━*/

using System.Collections;
using AimRacing.Ranking;
using UnityEngine;

public static class SessionCacheRefresher
{
    /// <summary>
    /// AimRacingPlayerName(前回のプレイヤー名)、GameManagerの計測タイム、SoundManagerのフェード状態、
    /// DebugKeyのポーズ状態をリセットしたうえで、Resources.UnloadUnusedAssets()とGC.Collect()で
    /// 未使用アセット・ゴミを回収します。ファイナライザ待ちのオブジェクトも含めて確実に回収し、
    /// セッションを繰り返すごとにガベージが積み重なっていかないようにします。
    /// </summary>
    public static IEnumerator Refresh()
    {
        AimRacingPlayerName.Clear(); //前のプレイヤーの名前を次セッションへ引き継がない
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResultTime = string.Empty; //前回の計測タイムを消去
            GameManager.Instance.SetLapTimes(null); //前回のラップタイムを消去
        }
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.ResetFadeState(); //フェード関連フラグ・コルーチンをリセット
        }
        DebugKey debugKey = UnityEngine.Object.FindFirstObjectByType<DebugKey>();
        if (debugKey != null)
        {
            debugKey.ResetPauseState(); //m_bPauseとTime.timeScaleの不整合を防ぐ
        }

        System.GC.Collect(); //UnloadUnusedAssetsが未参照アセットを正しく判定できるよう、先にマネージド側の参照を解放しておく
        yield return Resources.UnloadUnusedAssets(); //直前のプレイで不要になったアセットを解放

        //ファイナライザ待ちのオブジェクトは1回のCollectだけでは回収されずキャッシュとして残ることがあるため、
        //WaitForPendingFinalizers()を挟んで2回Collectし、蓄積したガベージを確実に回収する
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
    }
}
