# Nebula-Charon

[Nebula on the Ship](https://github.com/Dolly1016/Nebula) に第三陣営「カロン」を追加するアドオンです。

## 実装計画

1. Buskerと同じ偽装死状態、マップ内での復活、バイタル上の隠蔽、死亡確定処理を組み込む。
2. 偽装死ごとの最長距離を記録し、ステュクスの増減、上限、HUD表示を実装する。
3. 勝利ラインをまたいだときに資格を同期し、ゲーム終了判定で資格のあるカロンを勝者にする。
4. 日英の設定と役職辞典を追加し、導入用ZIPを作る。コンパイルと複数クライアントでの動作確認後にGitHub Releaseで配布する。

## 導入

`Charon.zip` を展開せずに、Nebulaを導入したAmong Usの `Addons/` に置きます。
ZIPの直下に `addon.meta`、`Scripts/`、`Language/` が必要です。
ゲーム設定の第三陣営でカロンを有効にできます。

## 仕様

- 偽装死のたびに最長距離をリセットします。最長距離の更新分に増加倍率を掛け、閾値と余剰量の合計を上限とします。余剰量の初期値は20です。
- 偽装死していないタスクフェーズでは、死亡確定後も毎秒1距離単位に減少倍率を掛けた量だけ減ります。会議と追放演出の間はゲージを停止します。
- 「時間制限」がOffのときに有効時間を適用し、Onのときにカウントダウンを無効にします。
- 勝敗のあるゲーム終了時に閾値以上のカロンが勝敗を上書きします。複数のカロンが条件を満たせば全員が対象です。
- 役職アイコンは設定していません。能力ボタンには本体のBusker用スプライトを使います。

## 開発と検証

参照ソースは [Nebula-Public Snapshot 26.11.04b](https://github.com/Dolly1016/Nebula-Public/commit/63ec897) です。
偽装死の状態管理とHUDに本体の内部APIを使用するため、`UseHiddenMembers` を有効にしています。Nebula更新時には再検証してください。

- 開発環境で `Charon/` を対象に `nos_check` を実行します。ゲーム内のエラーは `NebulaLog.txt` と `BepInEx/LogOutput.log` の両方で確認できます。
- 実機では停止、接近、最長距離未満の往復、最長距離更新、復活後の減少、会議中の停止を確認します。
- 有効時間の終了、通報、緊急会議、死体の消失、バイタルのOn/Offを確認します。
- ホストとゲストの両方で、閾値未満・閾値ちょうど・閾値以上・複数カロンの勝利結果を確認します。

## English

Charon is a neutral role for Nebula on the Ship. Feign death, travel farther from your corpse to build Styx, and revive within the map before a meeting or expiration. If Styx meets the threshold when the game ends, Charon takes the win. Put `Charon.zip` in the modded game's `Addons/` directory and enable Charon in the neutral role settings.
