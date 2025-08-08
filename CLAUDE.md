# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

JVLinkToSQLite は、JRA-VAN データラボが提供する競馬データを SQLite データベースに変換するツールです。

## ビルド・実行コマンド

### ビルド (Windows のみ)
```powershell
# Developer Command Prompt for VS 2022 を管理者権限で開いてから実行
nuget restore JVLinkToSQLite.sln
msbuild JVLinkToSQLite.sln /p:Configuration=Release /m

# 配布用パッケージ作成
powershell -ExecutionPolicy Bypass -File .\Build.ps1 -Package -BuildTarget Rebuild
```

### テスト実行
```powershell
# Visual Studio の Test Explorer から実行（NUnit 3 使用）
# または CLI から：
"%ProgramFiles(x86)%\Microsoft Visual Studio\2022\Professional\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" .\Test.Urasandesu.JVLinkToSQLite\bin\Release\Test.Urasandesu.JVLinkToSQLite.dll
```

### アプリケーション実行
```powershell
# 基本的な実行例
.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode Init  # 初期化（GUI）
.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode DefaultSetting -s setting.xml  # 設定ファイル生成
.\JVLinkToSQLite\bin\Release\JVLinkToSQLite.exe main --mode Exec -s setting.xml -d .\race.db -t 100  # データ変換実行
```

## アーキテクチャと重要な設計

### テクノロジースタック
- .NET Framework 4.8
- SQLite (System.Data.SQLite 1.0.118.0)
- DI コンテナ: DryIoc 5.4.1
- CLI パーサー: CommandLineParser 2.9.1
- テストフレームワーク: NUnit 3

### プロジェクト構成
- **JVLinkToSQLite**: メインアプリケーション（CLI エントリーポイント）
- **Urasandesu.JVLinkToSQLite**: コアロジック実装
- **Urasandesu.JVLinkToSQLite.Basis**: 基盤・共通ユーティリティ
- **Urasandesu.JVLinkToSQLite.JVData**: JV-Link データモデル定義
- **Test.Urasandesu.JVLinkToSQLite**: 統合テスト
- **Test.Urasandesu.JVLinkToSQLite.Basis**: 基盤層のユニットテスト
- **Test.Urasandesu.JVLinkToSQLite.JVData**: データ層のユニットテスト

### データ処理フロー
1. **JVLink からのデータ取得**: JVLinkWrapper 経由で競馬データを取得
2. **データ変換**: DataBridge パターンで各レコードタイプ（UM_UMA、CK_CHAKU など）を処理
3. **SQLite への書き込み**: トランザクション管理された一括挿入

### 主要コンポーネント
- **DataBridge**: JV データフォーマットから SQLite スキーマへの変換を担当
  - 各レコードタイプ（例：JV_UM_UMA、JV_CK_CHAKU）に対応する Bridge クラスが存在
  - DDL 生成とデータ挿入の両方を処理
- **JVLinkWrapper**: JV-Link API のラッパー層
- **Operator**: 処理フローの制御（JVDataToSQLiteOperator など）

### DuckDB 対応要件
requirements.md に DuckDB 同時出力機能の詳細な要件定義があります：
- SQLite と DuckDB への同時出力対応
- CLI オプション：`--db-target [sqlite|duckdb|both]`
- 型マッピングの最適化（DuckDB 向け）
- x64 固定でのビルド構成変更

## 開発時の注意事項

### テスト駆動開発
- NUnit を使用したテスト駆動開発を実施
- 各 DataBridge には対応するテストクラスが存在
- 新機能追加時は必ずテストから開始

### macOS での制限
- .NET Framework 4.8 のビルド・実行は Windows でのみ可能
- macOS ではコード編集のみ対応

### 依存関係
- JV-Link API への依存（JRA-VAN データラボ提供）
- ネイティブ SQLite ライブラリ（x86/x64）
- DuckDB.NET 追加時は x64 固定が必要