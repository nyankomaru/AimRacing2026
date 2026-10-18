/*━━━━━━━━━
@file    GameStateId.cs
@brief   新シーン遷移エンジンにおける唯一のState識別子
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/16
━━━━━━━━━*/

public enum GameStateId
{
    Splash,
    Opening,
    Menu,
    NameEntry,
    Intro,
    Racing,
    Result,
    Load //既存値の並び替えによるInspector上の整数値ズレ(Loading.unity等のm_leavingState等)を避けるため末尾に追加
}
