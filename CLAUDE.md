# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

JVLinkToSQLiteは、JRA-VAN データラボが提供する競馬データをSQLiteデータベースに変換するC#製コンソールアプリケーションです。

## ビルドコマンド

プロジェクトのビルドには、Developer Command Prompt for VS 2022を管理者権限で実行する必要があります。

### 基本的なビルドコマンド

```powershell
# NuGetパッケージの復元
nuget restore JVLinkToSQLite.sln

# ソリューション全体のビルド（Releaseモード）
msbuild JVLinkToSQLite.sln /p:Configuration=Release

# クリーンビルド
.\Build.ps1 -BuildTarget Clean
.\Build.ps1 -BuildTarget Rebuild

# パッケージ作成（デフォルト）
.\Build.ps1

# ドキュメント付きパッケージ作成
.\Build.ps1 -WithDocument
```

## テストの実行

テストフレームワークはNUnit 3.13.3を使用しています。

### Visual Studio内でのテスト実行
- Test Explorerを使用してテストを実行
- x86プラットフォームで実行（Test.Urasandesu.JVLinkToSQLite.runsettingsで設定済み）

### コマンドラインでのテスト実行
```powershell
# 特定のテストプロジェクトをビルド
msbuild Test.Urasandesu.JVLinkToSQLite\Test.Urasandesu.JVLinkToSQLite.csproj /p:Configuration=Debug

# NUnitコンソールランナーを使用する場合（別途インストールが必要）
# 例：nunit3-console Test.Urasandesu.JVLinkToSQLite\bin\Debug\Test.Urasandesu.JVLinkToSQLite.dll
```

## アーキテクチャ概要

### プロジェクト構成

1. **JVLinkToSQLite** - メインのコンソールアプリケーション
   - エントリーポイント
   - セットアップGUI（Windows Forms）

2. **Urasandesu.JVLinkToSQLite** - コアライブラリ
   - JVLinkWrappers: JRA-VAN APIのラッパー
   - DataBridges: 各種競馬データのマッピング処理
   - Operators: データ変換・処理のオペレーター

3. **Urasandesu.JVLinkToSQLite.Basis** - 基盤ライブラリ
   - 共通ユーティリティ
   - 基本的なデータ構造

4. **ObfuscatedResources** - 難読化されたリソース（ライセンス関連）

### 主要な技術要素

- **DI/IoC**: DryIoc 5.4.1を使用した依存性注入
- **データアクセス**: Entity Framework 6.4.4 + System.Data.SQLite 1.0.118.0
- **CLI**: CommandLineParser 2.9.1によるコマンドライン引数処理
- **コード生成**: T4テンプレート（.tt、.t4ファイル）による自動生成
- **モック**: NSubstitute 5.0.0を使用したテストダブル

### 開発時の注意点

1. **プラットフォーム**: x86で実行（JRA-VAN APIの制約）
2. **文字エンコーディング**: Shift-JISを扱うコードが多い（競馬データの仕様）
3. **T4テンプレート**: DataBridgeクラスの多くは自動生成されている
4. **テスト駆動開発**: 各DataBridgeやOperatorに対応するテストクラスが存在