# VRChat Discord Uploader

[![GitHub Release](https://img.shields.io/github/v/release/Takep/VRC-Discord-Uploader?color=blue&logo=github)](https://github.com/Takep/VRC-Discord-Uploader/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-blue.svg)](#動作環境)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

VRChatで撮影した写真をリアルタイムに自動検知し、指定したDiscordのチャンネル、スレッド、またはフォーラムへ投稿するWindows向けデスクトップアプリケーションです。

リアルタイムの撮影監視だけでなく、過去に撮影した写真を年月別に選択してまとめて送信する機能や送信待機キュー管理機能も備えています。

---

## 📥 ダウンロード

[**Releases ページ**](https://github.com/Takep/VRC-Discord-Uploader/releases) から最新バージョンをダウンロードしてください。

| 配布パッケージ | 特徴 | 対象 |
| :--- | :--- | :--- |
| **`VRChatDiscordUploader-Setup-x.x.x.exe`** | **インストーラー版（推奨）**<br>デスクトップやスタートメニューにショートカットを作成し、Windows起動時の自動常駐も設定可能 | 一般利用 |
| **`VRChatDiscordUploader-vx.x.x-win-x64.zip`** | **ポータブル版（ZIP）**<br>インストール不要。解凍して `VRChatDiscordUploader.exe` を直接実行 | ポータブル利用 |

> [!NOTE]
> 本アプリは必要なランタイムを内蔵（自己完結型）しているため、**.NET Runtimeの事前インストールは不要**です。ダウンロードしてそのままご利用いただけます。

---

## 🛡️ 初回起動時の注意（Windows SmartScreen警告）

本ソフトウェアは個人開発のオープンソースソフトウェアであるため、初回起動時にWindows SmartScreen（「WindowsによってPCが保護されました」という青い画面）が表示される場合があります。

**起動手順:**
1. 警告画面内の **「詳細情報」** をクリックします。
2. 表示される **「実行」** ボタンをクリックすると、通常通り起動します。

---

## ✨ 主な機能

- **📸 写真の自動監視・送信**
  - VRChat標準の写真保存フォルダ（`Pictures/VRChat`）を自動監視し、新しく撮影された写真を検知してDiscordへアップロードします。
  - 短時間に連続撮影された写真を1つのメッセージにまとめて送信する「まとめ送信（最大10枚）」と、撮影ごとに送信する「個別送信」を設定で切り替え可能です。
- **🖼️ 過去写真の手動・一括送信 & 写真アルバム**
  - アプリ画面へのドラッグ＆ドロップ、またはファイル選択ダイアログからの手動送信に対応。
  - 「写真アルバム」画面で過去の写真を年月別に一覧表示し、複数選択して一括送信できます。
- **📋 送信キュー管理**
  - 送信処理中の写真や待機中の写真をキュー画面でリアルタイムに確認可能。
- **📝 メタデータの自動付与と取捨選択**
  - VRChatのログファイルを解析し、「撮影日時」「ワールド名」「インスタンス情報」「同室のプレイヤー一覧」を自動抽出してEmbed（埋め込みカード）形式で投稿します。
  - 各メタデータ項目は設定画面で個別にON/OFFが可能です。また、文字を一切含めず写真のみを送信する「画像のみ送信モード」にも対応しています。
  - 同室プレイヤー数が多い場合でも、設定した最大表示人数（初期値: 15名）を超えると「+他○名」とスマートに省略表記されます。
- **📁 フォーラムの月別スレッド集約**
  - Discordのフォーラムチャンネル宛てに投稿する場合、月ごとにスレッド（例: `2026年09月の写真`）を自動作成してそこに写真をまとめていきます。
- **⚡ PNG形式を維持した高画質リサイズ**
  - Discordの容量上限（初期値: 8MB、設定で変更可能）を超える写真について、JPEG圧縮によるノイズ発生を避け、**PNG形式のままアスペクト比を維持して解像度を段階的に縮小**して送信します。
- **🪟 Windows 11準拠UI & タスクトレイ常駐**
  - WinUI 3によるモダンで使いやすいUI。
  - ウィンドウの「×」ボタンを押してもタスクトレイに常駐し、バックグラウンドで監視を継続します。
  - Webhook URL等の機密情報は、Windows標準の暗号化機能（DPAPI）を用いてローカルに安全に保存されます。

---

## 💻 動作環境

- **OS**: Windows 10 (バージョン 1809 以降) / Windows 11 (64-bit)
- **必須ランタイム**: 配布パッケージに内蔵されているため追加インストール不要

---

## 🚀 初期設定手順

1. **Discord Webhook URLの取得**:
   - 写真を投稿したいDiscordサーバーのチャンネル設定（歯車アイコン）を開きます。
   - 「連携」 > 「ウェブフック」から「新しいウェブフック」を作成し、「ウェブフックURLをコピー」をクリックします。
2. **本アプリケーションへの登録**:
   - アプリを起動し、左側メニューの「設定」を開きます。
   - 「Webhook URL」の入力欄に、コピーしたURLを貼り付けます。
   - 送信先の種類（チャンネル / 特定スレッド / フォーラム）を選択します。
   - 「接続テストを送信」をクリックし、Discordにテストメッセージが投稿されることを確認します。
   - 「設定を保存」をクリックします。
3. **利用開始**:
   - VRChat内で通常通りカメラ撮影を行うと、自動的にDiscordへ写真が投稿されます。

---

## 🛠️ ビルド方法（開発者向け）

### 前提条件
- .NET 10.0 SDK
- Windows 10 / 11 (x64)
- (任意) [Inno Setup 6](https://jrsoftware.org/isdl.php) （インストーラー作成時）

### ビルドとテスト
```powershell
# リポジトリのクローン
git clone https://github.com/Takep/VRC-Discord-Uploader.git
cd VRC-Discord-Uploader

# ソリューションのビルド
dotnet build VRChatDiscordUploader.sln

# ユニットテスト実行
dotnet test VRChatDiscordUploader.sln
```

### 配布パッケージの生成（ZIP & インストーラー）
同梱のビルドスクリプトを実行すると、テスト・自己完結型ビルド・ポータブルZIP・インストーラー（Inno Setup導入時）が `dist/` フォルダに出力されます。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1 -Version 1.0.0
```

---

## 📄 ライセンス

本ソフトウェアは [MIT License](LICENSE) のもとで公開されています。
