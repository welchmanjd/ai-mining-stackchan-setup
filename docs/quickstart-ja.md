# AIマイニングスタックチャン 超かんたん設定ガイド（Windows）

このページは、はじめての人向けです。  
上から順番に進めれば、初期設定ができます。

## 1. 準備するもの

- Windows 10 / 11 のPC
- M5Stack Core2 本体
- USBケーブル（データ通信対応）
- インターネット接続
- 必要な情報
  - Wi-Fi の SSID / パスワード
  - Duino-coin のユーザー名（マイニングを使う場合）
  - Azure Speech のキー / リージョン（必須）
  - OpenAI APIキー（必須）

## 1.1 USBドライバを入れる（最初に実施）

どちらのドライバか不明な場合は、次の2つを両方インストールしてください。

- CP210x ドライバ（Silicon Labs）
  - 案内ページ: https://docs.m5stack.com/en/download
  - 直接リンク: https://m5stack.oss-cn-shenzhen.aliyuncs.com/resource/drivers/CP210x_Windows_Drivers.zip
  - ファイル名: `CP210x_Windows_Drivers.zip`
- CH9102 ドライバ
  - 案内ページ: https://docs.m5stack.com/en/download
  - 直接リンク: https://m5stack.oss-cn-shenzhen.aliyuncs.com/resource/drivers/CH9102F.exe
  - ファイル名: `CH9102F.exe`

## 2. ダウンロードして起動する

1. 最新版のページを開く  
   https://github.com/welchmanjd/ai-mining-stackchan-setup/releases/latest
2. 画面の `Assets` から `AiStackchanSetup.zip` をクリックしてダウンロードする
3. 参考（v0.56の直接リンク）  
   https://github.com/welchmanjd/ai-mining-stackchan-setup/releases/download/v0.56/AiStackchanSetup.zip
4. ZIPを右クリックして展開する
5. 展開したフォルダ内の `AiStackchanSetup.bat` をダブルクリックする
6. M5Stack Core2 をUSBでPCにつなぐ

## 3. 画面の手順どおりに進める

基本は、各ステップで入力して `次へ` を押します。

### ステップ1: 接続

1. `探す` を押す
2. 自動で次の手順に進むのを待つ

見つからない場合:
- ケーブルを差し直す
- 別のUSBポートに挿す
- もう一度 `探す`

### ステップ2: 書き込み

1. `ファームウェアを上書き` が選ばれていることを確認する
2. `書き込み` を押す
3. 自動で次の手順に進むのを待つ

### ステップ3: 機能ON/OFF

初心者向け推奨:
- Wi-Fi: ON
- Mining: ON
- AI: ON

### ステップ4: Wi-Fi

1. SSID を入力
2. パスワードを入力
3. `次へ`

### ステップ5: Duino-coin

1. Duino-coin ユーザー名を入力
2. 必要な項目を入力
3. `次へ`

### ステップ6: Azure（必須）

1. Azureキーを入力
2. Azureリージョンを入力
3. `次へ`

### ステップ7: OpenAI（必須）

1. OpenAI APIキーを入力
2. `次へ`

### ステップ8: 追加設定

最初はデフォルトのままでOK。  
必要なら調整して `次へ`。

### ステップ9: 設定保存

1. `設定保存` を押す
2. 完了まで待つ
3. エラーがなければ `次へ`

### ステップ10: 完了

`完了` が表示されたら初期設定は終了です。

## 4. つまずいたとき

### 書き込みで失敗する

- USBケーブルを交換する（充電専用は不可）
- COMポートを選び直す
- もう一度 `書き込み`

### APIキー確認で失敗する

- 先頭/末尾の空白を消す
- コピー漏れがないか確認する
- ネット接続を確認する

### どうしても進まない

- アプリを再起動する
- USBを挿し直してステップ1からやり直す
- ログを添えて問い合わせる

## 4.1 ログ保存先

通常（`AiStackchanSetup.bat` から起動）:
- 展開したフォルダ直下の `log` フォルダ

例:
- `AiStackchanSetup\log\app.log`
- `AiStackchanSetup\log\flash_esptool.log`

補足:
- `AiStackchanSetup.exe` を単体で直接起動した場合は、環境によって `%LOCALAPPDATA%\AiStackchanSetup\Logs` が使われることがあります。

## 5. セキュリティ注意

- APIキーは他人に見せない
- 画面共有や動画撮影時はキーを隠す
- 公開前にキーを再発行すると安全

## 6. 動画版

動画版ガイド（YouTube）は準備中です。  
公開後、このページにURLを追加します。
