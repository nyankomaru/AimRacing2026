/*━━━━━━━━━
@file NameEntryState.cs
@brief 新GameFlowで名前入力シーンを管理
@author 24CU0312 久場洸太
@date 2026/09/07
最終更新日 2026/09/07
━━━━━━━━━*/

namespace AimRacing.Ranking
{
    public sealed class NameEntryState : GameStateBindingBase
    {
        public override GameStateId Id => GameStateId.NameEntry;
    }
}
