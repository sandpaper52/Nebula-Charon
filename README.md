# Nebula-Charon

[Nebula on the Ship](https://github.com/Dolly1016/Nebula) に第三陣営「カロン」を追加するアドオンです。

本アドオンは有志による非公式アドオンです。Nebula on the Shipの公式開発チームとは関係ありません。

## 導入

`Charon.zip` を展開せずに、Nebulaを導入したAmong Usの `Addons/` に置きます。
ZIPの直下に `addon.meta`、`Scripts/`、`Language/` が必要です。
ゲーム設定の第三陣営でカロンを有効にできます。

## 役職概要

### Charon

> 暗澹たる渡し守

カロンは、偽装死で幽霊になり、特殊ゲージ「ステュクス」をためて勝利を狙う第三陣営の役職です。

- **偽装死と復活** — 偽装死を使うと、その場に死体を残して幽霊として移動できます。死体が残っている間は、マップ内の現在地で復活できます。会議の開始や死体の消失で死亡が確定します。「偽装死の時間制限」がOnの場合は、有効時間が切れたときも死亡が確定します。
- **ステュクス** — 偽装死中に自分の死体から遠ざかり、その回の最長距離を更新した分だけ増えます。偽装死していないタスクフェーズ中は減少し、会議中は増減しません。
- **勝利条件** — ゲーム終了時にステュクスが必要量以上なら、カロンが勝利を奪います。死亡後も条件を満たせば勝利でき、複数のカロンが条件を満たした場合は全員が勝利します。

## 開発と検証

- 開発環境で `Charon/` を対象に `nos_check` を実行します。ゲーム内のエラーは `NebulaLog.txt` と `BepInEx/LogOutput.log` の両方で確認できます。
- 実機では停止、接近、最長距離未満の往復、最長距離更新、復活後の減少、会議中の停止を確認します。
- 有効時間の終了、通報、緊急会議、死体の消失、バイタルのOn/Offを確認します。
- ホストとゲストの両方で、閾値未満・閾値ちょうど・閾値以上・複数カロンの勝利結果を確認します。

## English

Nebula-Charon is an unofficial add-on for Nebula on the Ship. It is unaffiliated with the official Nebula on the Ship development team.

### Installation

Place `Charon.zip` in the `Addons/` folder of your Nebula on the Ship installation and leave it zipped. Enable Charon under Neutral Roles in the game settings.

### Role overview

#### Charon

> The Bleak Ferryman

Charon is a neutral role that feigns death to build up a gauge called Styx.

- **Feign Death and Revive** — Leave a corpse behind and move as a ghost. While the corpse remains, you can revive at your current location within the map. You die permanently if a meeting starts, your corpse disappears, or the timer expires while Feign Death Time Limit is enabled.
- **Styx** — While feigning death, you gain Styx only when you move farther from your corpse than you have during that attempt. During the task phase, Styx drains whenever you are outside feigned death. It stays the same during meetings.
- **Win condition** — If your Styx meets or exceeds the threshold when the game ends, you take the win, even if you are dead. If multiple Charons qualify, they all win.

## ライセンス / License

Nebula-Charon のソースコードと翻訳は GPL-3.0-only で配布します。ライセンス本文は [LICENSE](LICENSE) を参照してください。

Nebula-Charon’s source code and translations are licensed under GPL-3.0-only. See [LICENSE](LICENSE) for the full text.
