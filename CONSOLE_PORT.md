# CONSOLE_PORT — Steam/Switch 移植 進捗まとめ

このドキュメントは「Android/iOS 版を Steam・Switch へ移植する」大改修の進捗ログ。
アーキテクチャ上の不変条件は CLAUDE.md 第9〜11節に、iOS バージョン規約は
IOS_BUILD.md にある。ここは「何を・なぜ・どこまでやったか」と「残り」をまとめる。

最終更新: 2026-09-15

---

## 0. 移植のゴール（当初要件）

1. ヒロイン（フェゴール）台詞にボイスを付ける（声優発注。台詞は Talklist に
   フェゴール430行・約21,000文字。※テキスト確定 → 収録の順を厳守）
2. 広告を廃止し、代わりにコンティニュー回数制限を設ける
3. コントローラー/キーボード操作対応（選択枠の強調含む）

方針: **プロジェクトは分けない**。1リポジトリで、シーンだけ二本立て
（`Assets/Scenes/Console/` に同名コピー）、コードは `#if CONSOLE_BUILD` で分岐。
理由と詳細は CLAUDE.md 第10・11節。

---

## 1. 完了済み（このセッション）

### 基盤

- **セーブ層を遅延コミット方式に**（`35fc85d`, `0e8e55c`）
  - `SaveManager.Save()` は dirty フラグのみ。実書き込みは `SaveCommitter` が
    安全地点（シーン遷移・到着1F後・休止/フォーカス喪失/終了）で確定。
  - 物理I/Oは `ISaveBackend`（既定 `FileSaveBackend`）に隔離 → Switch 対応は
    バックエンド差し替えのみ。
  - 設定（音量・GameSettings）を `SettingsStore`（settings.json）に集約。
    `PlayerPrefs` 廃止（Switch に無いため）。旧キーからの移行あり。
  - 詳細と不変条件: CLAUDE.md 第9節。

- **広告分岐を `CONSOLE_BUILD` で切替可能に**（`4141684`）
  - コンティニュー判定は `ContinueGate`（`Assets/Script/Battle/ContinueGate.cs`）に
    集約。道中3回・ボス無限。残数リセットは `MainSceneRecovery`（街到着）。
  - 倉庫（道中1回）/SP振り直し（無制限）は各 `#if CONSOLE_BUILD` で広告スキップ。
  - GoogleMobileAds 参照は `Admanager.cs` 1ファイルに隔離維持（Switch で SDK除外）。
  - URLボタン（公式X/Discord）は `OpenUrlButton` が `#if UNITY_SWITCH` で自己非表示。
  - **CONSOLE_BUILD は未定義**（＝モバイル/エディタの挙動は不変）。定義した瞬間に
    コンソール仕様へ。詳細: CLAUDE.md 第10節。

- **コンソール版シーンの二本立て機構**（`35fa890`, `9348077`）
  - `Tools > コンソール版シーン` でビルド設定のシーンリストを一括切替
    （`ConsoleSceneSwitcher.cs`）。現在 **ビルド対象の全20シーンを複製済み**。
  - ⚠️ シーンのロジック変更（配線・コンポーネント追加）は両版に入れる（二重メンテ）。
    レイアウト（RectTransform）だけは各版独立。CLAUDE.md 第11節。

### コントローラー対応の共通部品

- `SelectionHighlighter.cs`（`066bc69`）: 自動生成の常駐。ナビ操作時だけ選択中
  Selectable に金枠を被せる。マウス/タッチでは非表示。`PreferredFallback`
  （シーンが指定する初期フォーカス）、`NavigationMode`（ナビ操作中フラグ）を公開。
- `ControllerNav.cs`（`a4e26a2`）: 明示ナビ配線の共有ヘルパー
  （`WireVerticalLoop` / `WireHorizontalLoop` / `SetNavigationNone` / `SetExplicit`）。
- `ModalFocusScope.cs`（`f3c9fe4`）: ポップアップのフォーカス封じ込め＋キャンセルキー
  （Esc/パッドB）。SetActive 開閉に追従・多重表示対応。`onCancel` が null なら
  キャンセル不可（コンティニュー確認など誤爆防止用）。
  ※uGUI ナビはレイキャストブロッカーを素通りするため、パネルで覆うだけでは
    裏のボタンへ移動できてしまう。これを引き戻すのが唯一の汎用手段。
- `ItemSlotView` / `GpShopCell`: 実行時に `Selectable`＋`ISubmitHandler` 付与。
  `ItemSlotView.SetItem` は空スロットを `Navigation.None`（空アイコンに枠が乗る対策）。
- `SimpleMenuNav.cs`（`2e93b93`）: 専用コントローラーの無いメニュー画面用の汎用。
  シーン内 Button を縦ループ配線＋初期フォーカス＋キャンセルで指定シーンへ。

### 画面別 コントローラー対応（済）

| 画面 | 主な内容 | commit |
|---|---|---|
| 戦闘 | 十字キーは右6コマンドの縦ループのみ（押せるボタンだけ毎F張り直し）。G=ギブアップ / O=ログ拡大（キー専用）/ 拡大中↑↓=ページ送り。ポップアップにモーダルスコープ | `21ba217` `f3c9fe4` `3cde7e8` |
| 戦闘中アイテム | フォーカスは格子のみ。1/2キー(パッドX/Y)で使用、Esc/Bで詳細閉→帰還 | `a7cecaf` ほか |
| タイトル | 右5コマンド縦ループ。初期化確認にモーダルスコープ | `3bc8afd` |
| メイン | 右6コマンド縦ループ（Gobutton実体を位置ベースで一括配線） | `a4e26a2` |
| ステータス | 振り分けグリッド動的配線。×→リセット→ポイント→詳細/基礎→× のループ。+100右で×へ飛ばない。初期フォーカス詳細/基礎。キャンセルで閉じる | `a4e26a2` `5f7d3a7` |
| 倉庫 | 所持品⇔戻る/詳細⇔倉庫。空/スクロールバー除外。詳細は固定スロット2D。選択毎に再配線。キャンセル2段 | `85a4761`〜`04d37e9` |
| GPショップ | グリッド位置配線（最上段↑→戻る）。詳細ポップアップにモーダルスコープ、中は横移動、キャンセル2段 | `56959cc` `542e6c0` |
| 会話図鑑 | 既読セル縦配線、選択セルを中央固定スクロール、キャンセルで戻る | `f084d68` |
| アイテム図鑑 | タブ＋戻るとグリッド配線、?セルもフォーカス可（詳細不可）、中央スクロール、初期フォーカス選択タブ左上（同期設定）。詳細=上下+キャンセル | `d277e42` `02808de` |
| モンスター図鑑 | アイテム図鑑と同方式。詳細=上下(モンスター送り)+左右(パネル切替)+キャンセル | `4d16f61` |
| Towerin | 階ボタン＋戻る配線、キャンセルでメイン | `2e93b93` |
| Itembox(拠点) | 倉庫と同方式（単一グリッド）。初期フォーカス左上アイテム | `2e93b93` `ff52639` |
| オプション | 初期フォーカスをBGMスライダーに | `542e6c0` |

### バランス/その他（このセッション中に依頼された個別修正）

- パッシブ重複減衰の切り捨てバグ修正（`PassiveCalculator.CalcWithDiminishing`、
  10未満のパッシブが複数所持で0になる問題）※セッション序盤。
- ラスボスHP引き継ぎ救済を解放後は全経路（タイトル経由含む）で表示可に（`c2e136b`）。
  未ロード時は `SaveManager.PeekFinalBossCarry`/`WriteFinalBossCarryEnabled` で
  該当フラグだけ read-modify-write（初期値全上書きの事故を回避）。

---

## 2. コントローラー対応の定石（次画面もこれで）

1. グリッドは**位置ベースで行にまとめて2D配線**（列数可変・飛びに強い）。
   Y降順→X昇順でソート、Y差20pxで行分割、`NearestByX`/`NearestByY` で隣接行/列へ。
2. **初期フォーカスは同期設定**する（`SelectionHighlighter.PreferredFallback`）。
   遅延コルーチンでのみ設定すると、初回入力・タブ切替時にフォールバックが先に走り
   別ボタンへ乗る（アイテム図鑑/Itemboxで実際に踏んだ）。
3. **詳細ウィンドウは `ItemDetailPanel.GetSlotButton(0..3)`（0=左上/1=右上/2=左下/
   3=右下）で固定スロット2D配線**。位置(Y)グルーピングは不安定で不採用。
4. **別アイテムへ切替時も再配線**（開閉検知だけだと前アイテムの配線が残る）。
5. スクロールリストは**選択セルを中央固定してリスト側をスクロール**（`CenterOn`）。
6. ポップアップは `ModalFocusScope.Attach(root, onCancel)`。ブロッカーは `SetNavigationNone`。
7. 表示専用UI（HPバー等）・未配線の死にボタンは `Navigation.None`。
8. キャンセルキーは Esc / パッド東(B)。詳細を持つ画面は2段（詳細閉→戻る）。

---

## 3. 残作業

### コントローラー/UI
- **Zukan トップに `SimpleMenuNav` を手動アタッチ**（Console/mobile 両 Zukan.unity）。
  cancelScene="Main"。専用スクリプトが無いためコードだけでは hook 不可。
- Console 版シーンの**レイアウト作り替え**（各画面。コード側は位置ベースなので
  レイアウトが変わっても追従する）。利き手UI削除・公式X/Discordリンク削除など。
- 選択枠の見た目調整（`SelectionHighlighter` 冒頭の定数）。
- 中央スクロールの「2段目固定」はビューポート中央で近似。厳密化は要望次第。

### プラットフォーム
- `CONSOLE_BUILD` を Standalone/Switch のスクリプティング定義シンボルに設定
  （現状未設定。設定でコンソール仕様へ切替）。**モバイルビルド前は必ず外す**。
- Switch: SDK除外（GoogleMobileAds/UGS Analytics）判断、セーブ領域マウント/コミットの
  `ISaveBackend` 実装、ロットチェック対応。開発者アカウント作成済み・申請は Steam と
  同内容で出して差分確認する方針。
- Steam: Steamworks 統合、解像度/ウィンドウ設定、Run In Background の扱い。
- Unity Pro/コンソールモジュールのライセンス確認（年商規模で無料の可能性）。

### ボイス
- フェゴール台詞テキスト**確定 → 声優発注 → 納品後に接続**。
  `TalkEvent.TalkLine` に AudioClip/voiceId を1フィールド追加、`TalkRunner` で再生、
  `AudioManager` に5本目のAudioSource＋音量。図鑑リプレイも同経路で自動対応。

---

## 4. 現在のバージョン

- 表示バージョン 1.2.0（Android versionCode 7 / iOS build 7、`520ff87`）。
- iOS の次バージョンは semver 継続（IOS_BUILD.md の採番規約参照）。
