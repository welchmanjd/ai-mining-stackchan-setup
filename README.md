## Quick Links

- Japanese quickstart: [docs/quickstart-ja.md](docs/quickstart-ja.md)
- Latest release download: [releases/latest](https://github.com/welchmanjd/ai-mining-stackchan-setup/releases/latest)

# AIマイニングスタックチャン セットアップ

M5Stack Core2 向けの AIマイニングスタックチャンに、ファームウェア書き込みと初期設定（Wi-Fi / APIキー等）を行う Windows 用セットアップアプリです。

## 概要

このアプリで実行できる主な作業:

- USB接続された M5Stack Core2 のポート検出
- ファームウェア書き込み
- 機能ON/OFF設定（Wi-Fi / Mining / AI）
- Duino-coin / Azure / OpenAI 設定の投入
- 設定保存・再起動
- サポート用ログ（ZIP）作成

## 動作環境

- Windows 10 / 11
- .NET 8 ランタイム（配布形態により不要な場合あり）
- USB接続可能な M5Stack Core2

## 使い方（配布物）

1. M5Stack Core2 を USB でPCに接続
2. 配布フォルダ直下の `AiStackchanSetup.bat` を実行
3. 画面の手順に沿って書き込み・設定

注意:

- `app` フォルダ配下は実行に必要です。移動・削除しないでください。
- `app` 直下の `AiStackchanSetup.exe` を直接起動せず、必ず `AiStackchanSetup.bat` から起動してください。

## ファームウェア差し替え

配布フォルダ直下の `firmware` フォルダに、以下条件のファイルを配置してください。

- ファイル名に `_public` を含む
- 拡張子が `.bin`

例: `stackchan_core2_public.bin`

同名のメタ情報ファイル（例: `stackchan_core2_public.meta.json`）があると、バージョン表示に利用されます。

## ログ

ログ出力先:

- 配布版 (`AiStackchanSetup.bat` 起動): `AiStackchanSetup.bat` と同じ階層の `log` フォルダ
- 開発実行時など（上記を解決できない場合）: `%LOCALAPPDATA%\AiStackchanSetup\Logs`

トラブル時は、アプリ内の「サポート用ログを作成」で ZIP を作成して共有してください。

## 開発者向け

### Step構成（責務分離）

セットアップ手順は `Steps` 配下で、以下のように責務を分離しています。

- `StepDefinitions.*.cs`
  - 各Stepの `Index / Title / Description / PrimaryActionText` を定義
- `Messages/Step/StepText.*.cs`
- `Messages/Ui/UiText.cs`
  - バリデーション文言、進捗文言、失敗/ガイダンス文言を分類して管理
- `*Step.cs`
  - 画面遷移単位のオーケストレーション（薄い制御）
- `RuntimeSettingsWorkflow.cs`
  - 設定保存Stepの実処理（API事前確認、送信/保存、反映確認、再起動ログ取得）
- `RuntimeWorkflowResult.cs`
  - ワークフロー処理結果の共通型

### ビルド

```powershell
dotnet build .\AiStackchanSetup.csproj -c Release
```

### 配布物作成

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_dist.ps1
```

## ライセンス

MIT License（`ai-mining-stackchan-core2` と同等）です。詳細は `LICENSE` を参照してください。
