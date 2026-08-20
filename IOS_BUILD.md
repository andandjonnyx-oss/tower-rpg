# IOS_BUILD.md

iOS 対応の作業記録と手順書。2026-08-19〜20 に Mac（MacBook Air M5）側で実施した内容。
Windows 側で作業する場合、**iOS 関連のビルドは Mac でしかできない**が、
コード修正は Windows でも可能なので、その際の前提としてこの文書を参照すること。

---

## 0. 現在地（2026-08-20 時点）

- ✅ Unity 6000.3.9f1 + iOS Build Support で **コンパイルエラーゼロ**
- ✅ iOS シミュレータで **起動・プレイ・ATT・AdMob テスト広告（報酬獲得まで）を実動作確認済み**
- ✅ Device SDK で **Archive 成功**（`build/TowerRPG.xcarchive`）
- ⏸ `.ipa` 書き出しは **Apple Distribution 証明書が未作成**のため停止中
- ⏸ TestFlight 配布・App プライバシー申告・Beta App Review は未着手

配布方式は **TestFlight（外部テスト）** を選択済み。理由は第6節。

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
- **配布は TestFlight（Ad Hoc ではない）**
  Ad Hoc は審査不要だが**テスターの UDID 入手**と **HTTPS ホスティング**が要る。
  TestFlight は初回に Beta App Review（1日程度）が要るが、以後の差し替えは審査なしで
  即反映でき、公開時の資産（アイコン・プライバシー申告）をそのまま流用できる。
- **Company Name は `miraigame` のまま**（変更していない）

---

## 7. 残作業

1. **Apple Distribution 証明書の作成** — アカウントに Admin 以上の権限が必要。
   ⚠️ 作成後は**キーチェーンアクセスから `.p12` で書き出してバックアップすること。**
   秘密鍵はこの Mac にしか無く、失うと同じ証明書で署名できなくなる。
2. `.ipa` 書き出し → App Store Connect へアップロード
3. **App プライバシー情報の入力**（Beta App Review の提出条件）
   - AdMob（IDFA・広告データ）と UGS Analytics（利用状況）の両方を申告する
   - Unity が生成する `PrivacyInfo.xcprivacy` の内容と整合させること
   - ⚠️ 申告内容は Google と Unity の公式ドキュメントで**必ず裏取りすること**
4. **`NSPrivacyTracking` の不整合解消（公開前）**
   生成されるマニフェストは `NSPrivacyTracking = false` / トラッキングドメイン 0 件だが、
   実際には ATT を要求し IDFA でパーソナライズ広告を配信している。
   Google の SDK は `NSPrivacyTracking` キー自体を持たず、アプリ側の宣言に委ねている。
   ⚠️ **単純に true にしてはいけない。** `NSPrivacyTrackingDomains` にドメインを列挙すると、
   ATT 拒否時に iOS がそれらへの通信を実際にブロックする。Google の公開ドメイン一覧を
   確認してから決めること。TestFlight のブロッカーではない。
5. **iOS テストデバイス ID の登録**（第2-1節）
6. ストア掲載素材（スクリーンショット・説明文）— TestFlight には不要、公開申請時に必要

### 検討事項（必須ではない）

- **画面の向きが片方向のみ**（`UISupportedInterfaceOrientations` = LandscapeRight のみ）。
  端末を逆さに持つと画面が追従しない。Android 版と同じ挙動だが、テスターから指摘が出やすい。
  両方向対応にするなら Player Settings > Resolution and Presentation で両方を許可する。
- **アプリアイコンは 512px を 200% 拡大したもの**。補間拡大なのでギザギザは出ず、
  実表示サイズでは判別できないが、元絵が高解像度で存在するなら再書き出しが望ましい。

---

## 8. Android 側への影響

**設定値の競合は無い。** `ProjectSettings.asset` の差分は iOS 専用キーのみで、
`applicationIdentifier.Android` も `AndroidBundleVersionCode` も無変更。
`GoogleMobileAdsSettings.asset` の `adMobAndroidAppId` も無変更。

⚠️ **唯一の共通コード変更は `AdManager` の初期化間接化**（第3-2節）。
Android でも広告初期化が1フレーム遅れる。**Android 実機でのスモークテストを推奨。**

文字コード変換（89ファイル）は Windows 側にとっても改善で、BOM 付き UTF-8 は
Windows の Unity / Visual Studio でも正しく読まれる。
