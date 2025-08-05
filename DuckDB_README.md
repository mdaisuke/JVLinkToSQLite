# DuckDB並行出力機能の使用方法

## 概要

JVLinkToSQLiteに、既存のSQLite出力と並行してDuckDBへの出力を行う機能を追加しました。

## 主な変更点

1. **IJVDataToDatabaseOperator インターフェース**: データベース操作を抽象化
2. **JVDataToDuckDBOperator**: DuckDB専用の実装
3. **JVDataToMultiDatabaseOperator**: SQLiteとDuckDBへの並列出力を管理
4. **設定ファイルの拡張**: DuckDB関連の設定項目を追加

## 設定方法

既存の設定ファイル（XML）に以下のDuckDB関連の設定を追加します：

```xml
<!-- DuckDB設定 -->
<DuckDBEnabled>true</DuckDBEnabled>
<DuckDBDataSource>JVData.duckdb</DuckDBDataSource>
<ContinueOnDuckDBError>true</ContinueOnDuckDBError>
```

- **DuckDBEnabled**: DuckDB出力を有効にするかどうか
- **DuckDBDataSource**: DuckDBデータベースファイルのパス
- **ContinueOnDuckDBError**: DuckDBエラー時に処理を継続するかどうか

## 実行方法

1. NuGetパッケージの復元（DuckDB.NETが追加されています）
   ```powershell
   nuget restore JVLinkToSQLite.sln
   ```

2. ビルド
   ```powershell
   msbuild JVLinkToSQLite.sln /p:Configuration=Release
   ```

3. 実行（DuckDB対応の設定ファイルを使用）
   ```powershell
   JVLinkToSQLite.exe --setting JVLinkToSQLiteSettingWithDuckDB.xml
   ```

## 動作仕様

- SQLiteとDuckDBへの出力は並列で実行されます
- SQLiteへの出力が失敗した場合、全体の処理が停止します
- DuckDBへの出力が失敗した場合、`ContinueOnDuckDBError`の設定に従います
  - `true`: エラーをログに記録して処理を継続
  - `false`: 処理を停止

## データ型の変換

SQLiteからDuckDBへのデータ型変換は以下のように行われます：

| SQLite | DuckDB |
|--------|---------|
| INTEGER PRIMARY KEY AUTOINCREMENT | BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY |
| TEXT | VARCHAR |
| REAL | DOUBLE |
| DATETIME | TIMESTAMP |

## 注意事項

1. **x86対応**: DuckDB.NETはx86環境でも動作しますが、事前の動作確認を推奨します
2. **メモリ使用量**: 両方のデータベースに出力するため、メモリ使用量が増加します
3. **ディスク容量**: 2つのデータベースファイルが作成されるため、十分なディスク容量が必要です

## トラブルシューティング

### DuckDBエラーが発生する場合

1. DuckDB.NETパッケージが正しくインストールされているか確認
2. 出力先ディレクトリへの書き込み権限を確認
3. `ContinueOnDuckDBError`を`true`に設定して、エラーの詳細をログで確認

### パフォーマンスの問題

1. `ThrottleSize`設定を調整してバッファサイズを最適化
2. 十分なメモリが利用可能か確認
3. ディスクI/O性能を確認