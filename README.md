# AIマイニングｽﾀｯｸﾁｬﾝ セットアップ

M5Stack Core2 向けの **AIマイニングｽﾀｯｸﾁｬﾝ** を、Windows からセットアップするアプリです。  
ファームウェア書き込みと初期設定（Wi-Fi / APIキーなど）をまとめて行えます。

---

## Quick Links

- はじめての方: [docs/quickstart-ja.md](docs/quickstart-ja.md)
- 最新版ダウンロード: [releases/latest](https://github.com/welchmanjd/ai-mining-stackchan-setup/releases/latest)

---

## はじめての方へ（最短手順）

まずは `docs/quickstart-ja.md` だけ読めばOKです。ここから始めてください。

- 超かんたん設定ガイド: [docs/quickstart-ja.md](docs/quickstart-ja.md)

補足:

- `ai-mining-stackchan-core2` は開発者向け（ソースコード）です。
- 利用者の方はセットアップアプリ（`AiStackchanSetup.bat`）から始めてください。

---

## 概要

このアプリでできること:

- USB接続された M5Stack Core2 のポート検出
- ファームウェア書き込み
- 機能 ON/OFF 設定（Wi-Fi / Mining / AI）
- Duino-coin / Azure / OpenAI 設定の投入
- 設定保存と再起動
- サポート用ログ（ZIP）作成

---

## 動作環境

- Windows 10 / 11
- .NET 8 ランタイム不要（配布版に同梱）
- USB接続可能な M5Stack Core2

※ 開発者向けにソースからビルドする場合は .NET 8 SDK が必要です。

---

## 使い方（配布物）

1. M5Stack Core2 を USB で PC に接続します。
2. 配布フォルダ直下の `AiStackchanSetup.bat` を実行します。
3. 画面の手順に沿って書き込み・設定を進めます。

注意:

- `app` フォルダ配下は実行に必要です。移動・削除しないでください。
- `app` 直下の `AiStackchanSetup.exe` は直接起動せず、必ず `AiStackchanSetup.bat` から起動してください。

---

## ファームウェア差し替え

配布フォルダ直下の `firmware` フォルダに、以下条件のファイルを置いてください。

- ファイル名に `_public` を含む
- 拡張子が `.bin`

例: `stackchan_core2_public.bin`

同名のメタ情報ファイル（例: `stackchan_core2_public.meta.json`）があると、バージョン表示に使われます。

---

## ログ

ログ出力先:

- 配布版（`AiStackchanSetup.bat` 起動）: `AiStackchanSetup.bat` と同じ階層の `log` フォルダ
- 開発実行時など（上記で確認できない場合）: `%LOCALAPPDATA%\AiStackchanSetup\Logs`

不具合時は、アプリ内の「サポート用ログを作成」で ZIP を作って共有してください。

---

## 開発者向け

### Step構成（責務分離）

セットアップ手順は `Steps` 配下で責務を分離しています。

- `StepDefinitions.*.cs`: 各 Step の `Index / Title / Description / PrimaryActionText` を定義
- `Messages/Step/StepText.*.cs`: Stepごとの文言を管理
- `Messages/Ui/UiText.cs`: バリデーション文言、進捗文言、失敗/ガイダンス文言を管理
- `*Step.cs`: 画面遷移単位のオーケストレーション（薄い制御）
- `RuntimeSettingsWorkflow.cs`: 設定保存 Step の実処理（API事前確認、送信/保存、反映確認、再起動ログ取得）
- `RuntimeWorkflowResult.cs`: ワークフロー処理結果の共通型

### ビルド

```powershell
dotnet build .\AiStackchanSetup.csproj -c Release
```

### 配布物作成

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_dist.ps1
```

---

## クレジット / 謝辞

ｽﾀｯｸﾁｬﾝは、ししかわさん（meganetaaan）が開発・公開されているコミュニケーションロボットです。  
作品ページ: [https://github.com/meganetaaan/stack-chan](https://github.com/meganetaaan/stack-chan)

本プロジェクト「AIマイニングｽﾀｯｸﾁｬﾝ」は、ｽﾀｯｸﾁｬﾝをはじめとした多くの成果を土台にしています。ありがとうございます。

- ｽﾀｯｸﾁｬﾝ: ししかわさん（meganetaaan）  
  [https://github.com/meganetaaan/stack-chan](https://github.com/meganetaaan/stack-chan)
- AIｽﾀｯｸﾁｬﾝ: Robo8080さん  
  [https://github.com/robo8080/AI_StackChan2](https://github.com/robo8080/AI_StackChan2)
- 筐体（タカオ版ボディ）: タカヲ（タカオ）さん（X: @mongonta555）  
  [BOOTH: mongonta.booth.pm](https://mongonta.booth.pm/)
- アバター（顔表示）: M5Stack-Avatar（作者: ししかわさん / meganetaaan）  
  [https://github.com/stack-chan/m5stack-avatar](https://github.com/stack-chan/m5stack-avatar)

---

## ライセンス

MIT License（`ai-mining-stackchan-core2` と同等）です。詳細は `LICENSE` を参照してください。
