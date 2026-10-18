/*━━━━━━━━━
@file    AppLog.cs
@brief   リリースビルドでログ呼び出し自体を除去するDebug.Logラッパー
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/03
         最終更新日 : 2026/09/03
━━━━━━━━━*/

using UnityEngine;

/// <summary>
/// UnityEngine.Debugの呼び出しをラップし、リリースビルドでは出力自体を消去するユーティリティ。
/// </summary>
public static class AppLog
{
    /// <summary>
    /// 通常ログを出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object _message)
    {
        Debug.Log(_message);
    }

    /// <summary>
    /// 通常ログをコンテキスト付きで出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    /// <param name="_context">Consoleクリック時にハイライトする対象オブジェクト</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object _message, Object _context)
    {
        Debug.Log(_message, _context);
    }

    /// <summary>
    /// 警告ログを出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object _message)
    {
        Debug.LogWarning(_message);
    }

    /// <summary>
    /// 警告ログをコンテキスト付きで出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    /// <param name="_context">Consoleクリック時にハイライトする対象オブジェクト</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object _message, Object _context)
    {
        Debug.LogWarning(_message, _context);
    }

    /// <summary>
    /// エラーログを出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object _message)
    {
        Debug.LogError(_message);
    }

    /// <summary>
    /// エラーログをコンテキスト付きで出力します。リリースビルドでは呼び出しごと除去されます。
    /// </summary>
    /// <param name="_message">出力するメッセージ</param>
    /// <param name="_context">Consoleクリック時にハイライトする対象オブジェクト</param>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object _message, Object _context)
    {
        Debug.LogError(_message, _context);
    }
}
