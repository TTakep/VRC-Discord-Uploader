# VRChat Discord Uploader

VRChatで撮影した写真を自動検知し、Discordのチャンネル・スレッド・フォーラムへ投稿するWindows向けデスクトップアプリです。  
ワールド名や同室プレイヤー情報のログ解析埋め込み、過去写真のアルバム送信、Discord制限に応じたPNG画像リサイズ機能などを備えています。

## ダウンロード

[Releases](https://github.com/TTakep/VRC-Discord-Uploader/releases) から最新版をダウンロードしてください。

- **`VRChatDiscordUploader-Setup-x.x.x.exe`**: インストーラー版（推奨）
- **`VRChatDiscordUploader-vx.x.x-win-x64.zip`**: ポータブル版（解凍してそのまま実行可能）

※ Windows 10 (1809以降) / Windows 11 (64bit) 対応。.NETランタイムの事前インストールは不要です。  
※ 初回起動時にSmartScreen警告画面が表示された場合は、「詳細情報」→「実行」をクリックしてください。

## 主な機能

- **写真の自動監視・送信**: 撮影された写真を検知して自動アップロード（個別送信 / 最大10枚まとめ送信）
- **メタデータ自動抽出**: ログからワールド名・インスタンス・同室プレイヤー名を解析し、Embedカードとして投稿
- **過去写真アルバム**: 撮影年月ごとに写真を一覧表示し、複数選択して送信
- **送信キュー**: アップロード待機中の写真のステータス管理
- **フォーラム対応**: 月別スレッド（例: `2026年09月の写真`）への自動集約
- **PNGリサイズ**: Discordの容量上限を超える場合、PNG形式とアスペクト比を保ったまま段階的に解像度縮小
- **タスクトレイ常駐**: ウィンドウを閉じてもバックグラウンドで監視を継続

## 使い方

1. Discordで投稿先チャンネルの Webhook URL を作成・コピーします。
2. アプリの「設定」を開き、Webhook URLを入力して送信先種別（チャンネル / スレッド / フォーラム）を選択します。
3. 「接続テストを送信」で確認後、「設定を保存」をクリックします。
4. あとはVRChat内で写真を撮ると自動で送信されます。

## 開発・ビルド

- 動作環境: .NET 10.0 SDK, Windows 10/11 x64, Inno Setup 6（インストーラー作成時）

```powershell
# ビルド & テスト
dotnet build VRChatDiscordUploader.sln
dotnet test VRChatDiscordUploader.sln

# 配布パッケージ作成 (dist/ にインストーラーとZIPを出力)
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1 -Version 1.0.0
```

## ライセンス

[MIT License](LICENSE)
