AIマイニングスタックチャン セットアップ
========================================

このツールは、AIマイニングスタックチャン（M5Stack Core2）に
ファームウェアを書き込み、Wi-Fi や API キー等の初期設定を投入するための
Windows用セットアップアプリです。

はじめに（重要）
----------------
このフォルダ内の「AiStackchanSetup.bat」から起動してください。
app フォルダ内のファイルは必須部品なので、移動・削除しないでください。

使い方（概要）
--------------
1) USBで M5Stack Core2 をPCに接続します。
2) 「AiStackchanSetup.bat」を起動します。
3) 画面の手順に従って、ファーム書き込みと設定を行います。
4) 完了後、AIマイニングスタックチャンが起動します。

ファームウェアについて
----------------------
この配布物には、書き込み用のファームウェアが同梱されています。
ファームウェアを差し替えたい場合は、
このフォルダの「firmware」フォルダに、次の条件を満たすファイルを置いてください：

- ファイル名に "_public" を含む
- 拡張子が .bin

例：stackchan_core2_public.bin
（この firmware フォルダが優先的に使用されます）

ログ
----
ログは次のフォルダに保存されます：
AiStackchanSetup.bat と同じ階層の log フォルダ
（例: AiStackchanSetup\log\app.log）

トラブル時
----------
・うまくいかない場合は、アプリ内の「サポート用ログを作成」ボタンでZIPを作成し、
  そのZIPをサポート窓口に提供してください。
・ファームウェアが見つからない場合は、次を確認してください：
  - このフォルダ直下に firmware フォルダがある
  - firmware フォルダ内に _public を含む .bin がある
  - AiStackchanSetup.bat から起動している（app 内の exe を直接起動しない）

クレジット / 謝辞
----------------
ｽﾀｯｸﾁｬﾝは、ししかわさん（meganetaaan）が開発・公開されている、
手のひらサイズのｽｰﾊﾟｰｶﾜｲｲコミュニケーションロボットです。
作品ページ: https://github.com/meganetaaan/stack-chan

AIｽﾀｯｸﾁｬﾝ: Robo8080さん https://github.com/robo8080/AI_StackChan2
筐体（タカオ版ボディ）: タカヲ（タカオ）さん（X: @mongonta555）
（関連情報まとめ）https://mongonta.booth.pm/
アバター（顔表示）: M5Stack-Avatar（作者：ししかわさん / meganetaaan）
https://github.com/stack-chan/m5stack-avatar
