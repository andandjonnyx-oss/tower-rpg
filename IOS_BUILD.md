# IOS_BUILD.md

iOS 対応の作業記録と手順書。2026-08-19〜20 に Mac（MacBook Air M5）側で実施した内容。
Windows 側で作業する場合、**iOS 関連のビルドは Mac でしかできない**が、
コード修正は Windows でも可能なので、その際の前提としてこの文書を参照すること。

---

## 0. 現在地（2026-08-24 時点）

**バージョン 1.0（ビルド2）を App Store 審査へ提出済み。結果待ち。**

これまでに完了したこと:

- ✅ Unity 6000.3.9f1 + iOS Build Support で **コンパイルエラーゼロ**
- ✅ iOS シミュレータで **起動・プレイ・ATT・AdMob テスト広告（報酬獲得まで）を実動作確認済み**
- ✅ **Apple Distribution 証明書を作成**（2026-08-20。クラウド管理方式。第7節）
- ✅ **TestFlight でビルド1を配信し、外部テスターの iPhone 実機で検証完了**
  （テスト期間中は `UseIosTestAdUnitId = true` でテスト広告を配信）
- ✅ **App Store Connect のメタデータ入力は完了**（アプリ情報 / アプリのプライバシー /
  バージョン情報 / App Review 情報 / スクリーンショット5枚）。第7節参照
- ✅ **`NSPrivacyTracking` の扱いは調査完了し、「対応不要」で確定**（第7節）
- ✅ **`app-ads.txt` 設置済み**（第7節「完了」6）
- ✅ **Android 実機スモークテスト済み**（`AdManager` の `Update()` 経由化にリグレッション無し）
- ✅ **公開ビルド（1.0 / build 2）を提出**。`UseIosTestAdUnitId = false` に戻し、
  本番広告IDが使われることを IL2CPP のメタデータ解析で検証済み（検証手順は第7節）

⚠️ 提出前に **TestFlight の外部テスト用ビルドを削除済み**。これをやらないと、
審査用にアップロードしたビルド（本番広告入り）が外部テスターにも自動配信され、
タップされると AdMob アカウント停止のリスクがある。**次回以降の更新時も同じ手順が必要。**

配布方式は **TestFlight（外部テスト・パブリックリンク）** を採用。理由は第6節。

---

## 1. Mac 側の環境

| 項目 | バージョン / 備考 |
|---|---|
| macOS | 26.4 (arm64 / M5) |
| Xcode | 26.6（App Store 版）+ Command Line Tools 26.6 |
| iOS SDK | 26.5 / シミュレータランタイム 26.5 (arm64) |
| Unity | **6000.3.9f1**（`ProjectVersion.txt` と完全一致必須）+ iOS Build Support (Apple silicon) |
| CocoaPods | 1.17.0（Homebrew 経由。`sudo gem install` は使わない） |
| その他 | Homebrew / git 2.55 / git-lfs / gh |

### ⚠️ 環境構築で必須だが忘れやすいもの

- **Rosetta 2**（`softwareupdate --install-rosetta --agree-to-license`）
  Unity Editor は Apple Silicon ネイティブ版でも内部ツールが x86_64 のため Rosetta を要求する。
  未導入だと **Unity が不可視のモーダルダイアログを出したまま無反応になる**（第5節参照）。
- **`/usr/local/bin/pod` シンボリックリンク**
  `sudo ln -sf /opt/homebrew/bin/pod /usr/local/bin/pod`
  Unity を Finder / Unity Hub から起動すると `/opt/homebrew/bin` が PATH に入らず、
  EDM4U が `pod: command not found` で失敗する。iOS ビルド最頻出の罠。
- **`export LANG=en_US.UTF-8`**（`~/.zprofile`）
  CocoaPods が非 UTF-8 ロケールで警告を出す。日本語ファイル名を含むため設定しておく。
- **`git config --global core.precomposeunicode true`**
  macOS はファイル名を NFD で扱う。未設定だと日本語アセット名が Windows と別ファイル扱いになる。

---

## 2. iOS 対応で追加・変更したもの

### 2-1. AdMob（iOS 用 ID）

Android の ID は iOS では動作しないため、AdMob 管理画面で iOS アプリを別途登録した。

- `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`
  - `adMobIOSAppId: ca-app-pub-7063976043351494~7919679548`
  - `userTrackingUsageDescription`（ATT 説明文。第2-2節と同じ文言）
- `Assets/Script/Status/Admanager.cs`
  - `IosRewardedAdUnitId = "ca-app-pub-7063976043351494/3825356010"`

⚠️ `adMobIOSAppId` が**空のままだと iOS 起動時にクラッシュする**。

⚠️ `TestDeviceIds` には現在 Pixel 7a のみ登録。**iPhone 実機で確認する際は、
初回起動時にコンソールへ出る iOS 端末の ID を必ず追加すること。**
登録せずに本番広告をタップすると無効トラフィック判定で AdMob アカウント停止のリスクがある。

### 2-2. ATT（App Tracking Transparency）

iOS 14 以降、IDFA を使う広告には許諾ダイアログが必須。**無いと審査でリジェクトされる。**
Google の公式ドキュメントが採るネイティブプラグイン方式で実装した。

| ファイル | 役割 |
|---|---|
| `Assets/Plugins/iOS/ATTPlugin.mm` | `ATTrackingManager` を叩くネイティブ層 |
| `Assets/Script/Status/AppTrackingTransparency.cs` | C# ラッパー（コルーチン） |
| `Assets/Editor/IosPostProcessBuild.cs` | framework の weak link 追加 + `Info.plist` への書き込み |

Info.plist の説明文（審査で読まれる）:

> あなたの興味に合わせた広告を表示するために、デバイスの識別子を使用します。
> 許可しない場合も広告は表示されますが、内容が最適化されません。

⚠️ **この文言は `IosPostProcessBuild.cs` の定数と `GoogleMobileAdsSettings.asset` の
2箇所にある。変更するときは両方を揃えること。**

### 2-3. その他

- `ProjectSettings.asset`
  - `applicationIdentifier.iPhone: com.mirailoveratory.towerrpg`（Android と同一 ID）
  - `buildNumber.iPhone: 1`
  - `targetDevice: 0`（**iPhone Only**。理由は第6節）
  - iOS アイコン 19 スロット（`Assets/Art/AppIcon/icon_ios_1024.png`）
- `Assets/Editor/IosIconSetup.cs` — 1枚から19スロットを埋めるエディタ拡張
  （Tools > iOS > アプリアイコンを一括設定）
- `Assets/Editor/IosPostProcessBuild.cs` — `ITSAppUsesNonExemptEncryption = false`

---

## 3. 設計上の不変条件（変更するとき注意）

### 3-1. 広告初期化の順序: UMP 同意 → ATT → MobileAds.Initialize()

`AdManager.RequestAttThenInitialize()` がこの順序を保証している。
GDPR 側（UMP）を先に処理したうえで、広告 SDK が IDFA を掴む前に ATT を確定させる必要がある。
**この順番を崩さないこと。**

### 3-2. ⚠️ `InitializeAdsIfAllowed()` はフラグを立てるだけ（Android にも影響する変更）

UMP のコールバックは**バックグラウンドスレッドで来ることがあり、そこから
`StartCoroutine` は呼べない**。そのため `volatile bool pendingAdsInitialize` を立てて
`AdManager.Update()` がメインスレッドで拾う構造にした。

**この変更は Android 版にも影響する**（広告の初期化が1フレーム遅れる）。
機能的には等価で `adsInitialized` ガードも残っているが、
広告まわりを触るときはこの間接化を意識すること。

### 3-3. ATT の結果取得はコールバックではなくポーリング

`ATTPlugin.mm` は結果を C# に返さない。C# 側が `GetTrackingAuthorizationStatus()` を
コルーチンで監視する。**IL2CPP での関数ポインタマーシャリング（MonoPInvokeCallback）は
AOT 制約を踏みやすいため意図的にこの設計にしている。** コールバック方式に書き換えないこと。

### 3-4. framework は UnityFramework ターゲットにも追加する

Unity 2019.3 以降、`.mm` は **UnityFramework 側にコンパイルされる**。
メインターゲットだけに framework を足すとリンクエラーになる。
`IosPostProcessBuild.cs` は両方に追加している。weak link にしているのは iOS 14 未満対策。

### 3-5. ATT はアプリがアクティブになってから要求する

アクティブ化前に要求するとダイアログが出ず NotDetermined のまま握り潰される。
`AppTrackingTransparency.RequestIfNeeded()` はフォーカス待ち + 3秒後の1回リトライを入れている。

---

## 4. ビルド手順

### 4-1. 実機 / TestFlight 向け

1. Unity: Player Settings > iOS > Other Settings > **Target SDK = Device SDK**
2. Unity: Build → 出力先 `~/dev/tower-rpg/build`
3. Archive（署名なし。理由は第5-4節）:

```bash
cd ~/dev/tower-rpg/build
xcodebuild -workspace Unity-iPhone.xcworkspace -scheme Unity-iPhone \
  -configuration Release -destination 'generic/platform=iOS' \
  -archivePath ~/dev/tower-rpg/build/TowerRPG.xcarchive \
  -derivedDataPath ./DerivedDataArchive \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" \
  archive
```

4. `.ipa` 書き出し（配布用署名を付与。`exportOptions.plist` は `build/` に用意済み）:

```bash
cd ~/dev/tower-rpg/build
xcodebuild -exportArchive -archivePath TowerRPG.xcarchive \
  -exportPath export -exportOptionsPlist exportOptions.plist \
  -allowProvisioningUpdates
```

### 4-2. シミュレータ検証（実機不要。iPhone を持っていない場合の確認手段）

1. Unity: **Target SDK = Simulator SDK**、**Simulator Architecture = arm64**
   （arm64 にしないと第5-3節の差し替えが必要になる）
2. 出力先は `build-sim`（`.gitignore` 済み）
3. ビルドとインストール:

```bash
xcrun simctl boot <UDID>
cd ~/dev/tower-rpg/build-sim
xcodebuild -workspace Unity-iPhone.xcworkspace -scheme Unity-iPhone \
  -configuration Debug -sdk iphonesimulator \
  -destination 'generic/platform=iOS Simulator' \
  -derivedDataPath ./DerivedData \
  ARCHS=arm64 ONLY_ACTIVE_ARCH=NO \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
xcrun simctl install <UDID> DerivedData/Build/Products/Debug-iphonesimulator/ProductName.app
xcrun simctl launch --console-pty <UDID> com.mirailoveratory.towerrpg
```

⚠️ Unity の `Debug.Log` は os_log ではなく stdout に出る。`--console-pty` を付けないと
`[ATT]` `[AdManager]` 等のログが取れない。

⚠️ シミュレータでは **IDFA が常にゼロ値**。ATT のフロー確認はできるが、
「許可」時の実際のトラッキング動作は実機でしか検証できない。

⚠️ **Simulator SDK ビルドは App Store に提出できない。** リリース時は Device SDK に戻すこと。

---

## 5. 踏んだ罠と原因（再発防止）

### 5-1. Unity が起動せず無反応（Rosetta 2 未導入）

プロセスは生きているが CPU 0%、`Library/` も `Editor.log` も生成されない。
`sample <pid>` を取ると `RunRosettaInstallationHelperAndQuitImpl` → `NSAlert runModal` で
停止していた。**Rosetta インストールを促すモーダルが Unity Hub の背後に隠れていた。**

→ Unity が「反応しない」ときは `⌘ + Tab` で隠れたダイアログを探す。
→ `softwareupdate --install-rosetta --agree-to-license` で解決。

### 5-2. `.cs` が Shift-JIS で macOS のコンパイルが全滅

`Unexpected character '???'` / `Unrecognized escape sequence` / `Unterminated string literal`
が大量発生。**BOM 無しファイルの文字コード推定は Windows では CP932、macOS では UTF-8 に
フォールバックする**ため、同じファイルが Windows で読めて macOS で壊れる。

→ 88ファイルを Shift-JIS → **BOM 付き UTF-8 + LF** に変換して解決（`CLAUDE.md` 第7節）。
→ **BOM 付き UTF-8 は Windows でも正しく読まれる**ので、両環境で安全になった。

### 5-3. シミュレータで `Failed to find matching arch` / arm64 リンクエラー

Unity の **Simulator Architecture 設定が x86_64** だと、生成プロジェクトに
x86_64 版のバイナリだけがコピーされる。M5 のシミュレータランタイムは arm64 なので動かない。
`ARCHS=arm64` で上書きすると、今度は Unity 事前ビルド済みライブラリだけ x86_64 のまま
残ってリンクが破綻する。

対象は2つ。Unity 同梱のユニバーサル版に差し替えれば通る:

| ファイル | 差し替え元 |
|---|---|
| `Libraries/baselib.a` | `PlaybackEngines/iOSSupport/Trampoline/Libraries/baselib-sim-x64arm64.a` |
| `Frameworks/UnityRuntime.framework` | `PlaybackEngines/iOSSupport/Trampoline/Frameworks/libiPhone-lib-sim-x64arm64/` |

→ 恒久対応は **Unity の Simulator Architecture を arm64 にする**こと。

### 5-4. Archive が「デバイスが無い」で失敗する

```
Your team has no devices from which to generate a provisioning profile.
No profiles for 'com.mirailoveratory.towerrpg' were found
```

Xcode の自動署名は Archive 時に**まず「開発用（iOS App Development）」プロファイル**で
署名しようとするが、開発用プロファイルは**最低1台の登録デバイスが必須**。
iPhone 実機を持っていないと作成できない。

| プロファイル種別 | デバイス登録 |
|---|---|
| iOS App Development | **1台以上必須** |
| Ad Hoc | **登録した端末のみ** |
| **App Store 配布用** | **不要** |

→ **署名なしで Archive し、`-exportArchive` 時に配布用署名を付ける**（第4-1節）。
CI で標準的に使われる方法。

### 5-5. `LaunchScreen-iPad.storyboard` が見つからない

Target Device を iPhone Only に変更したあと、Unity が iPad 用ランチスクリーンを
生成しなくなった一方、`build/` に残っていた既存 Xcode プロジェクトが参照を保持していた。

→ **Unity の iOS ビルドは既存フォルダに追記（Append）する。**
Target Device / Bundle ID / 対応 OS バージョンなど**プロジェクト構造に影響する設定を
変更したときは、出力先を削除するか Unity のビルドダイアログで Replace を選ぶこと。**

---

## 6. 判断の記録（なぜそうしたか）

- **Bundle ID は Android と同一の `com.mirailoveratory.towerrpg`**
  ⚠️ App Store Connect に登録すると**変更できない**。
- **Target Device = iPhone Only**
  iPad 対応を宣言すると iPad 用スクリーンショットが必須になり、iPad でも審査される。
  このゲームは横向き固定で `CLAUDE.md` 第5節のとおり 1920×1080 中央固定の設計なので、
  iPad の 4:3 では余白配分が想定外になる。**後から Universal へ変更は可能**なので、
  まず iPhone で公開する。iPad ユーザーは iPhone 互換モードでインストールできる。
- **外部テスターへはパブリックリンクで配布**（2026-08-21 決定）
  テスターは知人で、App Store Connect のチームメンバーではない。
  **内部テストは相手を ASC ユーザーに登録する必要があり**（Account Holder / Admin /
  App Manager / Developer / Marketing のいずれかの役割）、アカウントの中身への
  アクセスを与えてしまうので不採用。外部テストのパブリックリンクなら
  **URL を渡すだけ**で済み、相手のメールアドレスすら不要。上限は 10,000 人、
  テスター1人あたり 30 台まで。ビルドの有効期限は 90 日。
- **配布は TestFlight（Ad Hoc ではない）**
  Ad Hoc は審査不要だが**テスターの UDID 入手**と **HTTPS ホスティング**が要る。
  TestFlight は初回に Beta App Review（1日程度）が要るが、以後の差し替えは審査なしで
  即反映でき、公開時の資産（アイコン・プライバシー申告）をそのまま流用できる。
- **Company Name は `miraigame` のまま**（変更していない）

---

## 7. 残作業

### 🔁 バージョン更新時の手順（毎回必要）

1. **TestFlight の外部テスト用ビルドを削除する。** 審査用にアップロードしたビルドは
   TestFlight にも現れ、外部テストグループへ自動配信される。本番広告入りビルドが
   テスターに届くとタップされ、AdMob アカウント停止のリスクがある。
2. **`AdManager.UseIosTestAdUnitId` を `false` にする。** TestFlight でテスターに
   配布する期間だけ `true` にし、**公開ビルドでは必ず `false` に戻す**。
   戻し忘れると iOS の広告収益がゼロになる。
3. **`buildNumber.iPhone` を上げる。** Apple は同一バージョン内での同じビルド番号を
   拒否する。Unity の GUI で変更したら **File > Save Project かビルド実行までは
   `ProjectSettings.asset` に書かれない**ので、ディスク上の値を確認すること。
4. Unity でビルド → 第4-1節の Archive / 書き出し → 下記の検証 → Transporter でアップロード。

### ✅ 本番広告IDが使われることの検証手順

⚠️ **`strings global-metadata.dat | grep ca-app-pub` では判定できない。**
C# の `const string` はコードから参照されなくても**アセンブリのメタデータに定数値として
保持される**ため、テスト用IDも本番IDも常に出てくる。

正しい判定は「**文字列リテラルデータ領域の中に出現するか**」で行う。
`UseIosTestAdUnitId` は `const bool` なので Roslyn が三項演算を定数畳み込みし、
実際に使われる方だけがリテラルとして残る。

```python
import struct
d = open('Payload/<ProductName>.app/Data/Managed/Metadata/global-metadata.dat', 'rb').read()

# ⚠️ ヘッダの並びは metadata version で変わる。値を決め打ちせず、下の自己検証を必ず通すこと。
# 2026-08-24 の実測（metadata version 39 / Unity 6000.3.9f1）:
#    8: stringLiteralOffset      = 380
#   12: stringLiteralSize        = 66,752   （8バイト × 8,344 件）
#   16: （オフセットではない）    = 16,688   ← 件数の2倍。ここを領域先頭と誤読しやすい
#   20: stringLiteralDataOffset  = 67,132
#   24: stringLiteralDataSize    = 531,311
litOff      = struct.unpack_from('<i', d,  8)[0]
litSize     = struct.unpack_from('<i', d, 12)[0]
litDataOff  = struct.unpack_from('<i', d, 20)[0]
litDataEnd  = litDataOff + struct.unpack_from('<i', d, 24)[0]

# --- 自己検証: 基準オフセットが正しいかを、リテラルテーブルとの整合で確認する ---
# 対象文字列の絶対位置から基準を引いた値が、テーブルのどれかの dataIndex と一致するはず。
# 一致しなければヘッダの読み方が違うので、判定結果を信用してはいけない。
def validate(s: bytes) -> bool:
    di = d.find(s) - litDataOff
    return any(struct.unpack_from('<II', d, litOff + i*8)[1] == di
               for i in range(litSize // 8))

for label, s in [("本番  ", b"ca-app-pub-7063976043351494/3825356010"),
                 ("テスト", b"ca-app-pub-3940256099942544/1712485313")]:
    i = d.find(s)
    inside = i >= 0 and litDataOff <= i < litDataEnd
    print(f"{label} offset={i} -> "
          f"{'領域内=コードが参照している' if inside else '領域外=const定数値のみ'}"
          f"{' [テーブル整合OK]' if inside and validate(s) else ''}")
```

⚠️ **オフセット 16 を領域先頭として読まないこと。** これは件数の2倍であって
オフセットではない。誤読すると領域が 16,688〜83,820 になり、**本番IDもテスト用IDも
どちらも「領域外」と判定され、判定自体が無意味になる**（2026-08-24 に実際に発生）。

⚠️ ヘッダの並びは Unity / metadata version で変わりうる。上の自己検証（`validate`）を
必ず通し、**「領域内」と出た文字列がテーブルの `dataIndex` とも一致すること**を確認する。
一致しなければ読み方が違う。

**より確実なのは生成 C++ を見ること。** オフセット計算に依存しないので、こちらを
一次判定にするのが安全。
`Il2CppOutputProject/Source/il2cppOutput/Assembly-CSharp__2.cpp` の
`AdManager_get_RewardedAdUnitId_...` が**どちらのリテラルを返しているか**を確認する
（三項演算が残っていなければ定数畳み込み済み）。

前提として `UseIosTestAdUnitId` は `const bool` なので、Roslyn の定数畳み込みにより
**使われない側の `ldstr` は生成されない**（C# の言語仕様として保証される）。
つまりソースが `false` ならビルドは必ず本番IDを使う。バイナリ解析は
「ビルドしたソースが本当に `false` だったか」を事後確認するための裏取りに過ぎない。

2026-08-24 のビルド2での実測（再検証済み）:

```
リテラルデータ領域 = 67,132 〜 598,443
  本番ID   offset   502,340  → 領域内（リテラルテーブル index 6165 / dataIndex 435,208 と一致）
  テストID offset 6,557,218  → 領域外（const 定数値のみ）
```

生成 C++ 側でも `AdManager_get_RewardedAdUnitId_...` が単一リテラルを返しており、
**提出したビルド2が本番広告IDを使うことは二重に確認済み。**

---

## 8. Android 側への影響

**設定値の競合は無い。** `ProjectSettings.asset` の差分は iOS 専用キーのみで、
`applicationIdentifier.Android` も `AndroidBundleVersionCode` も無変更。
`GoogleMobileAdsSettings.asset` の `adMobAndroidAppId` も無変更。

⚠️ **唯一の共通コード変更は `AdManager` の初期化間接化**（第3-2節）。
Android でも広告初期化が1フレーム遅れる。**Android 実機でのスモークテストを推奨。**

文字コード変換（89ファイル）は Windows 側にとっても改善で、BOM 付き UTF-8 は
Windows の Unity / Visual Studio でも正しく読まれる。
