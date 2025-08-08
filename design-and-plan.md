# DuckDB 同時出力対応 設計書および開発フェーズ分割

## 目的と方針
- SQLite 出力の既存フローに DuckDB を同時出力として追加し、単一パース結果を 2 系へ同報。
- 後方互換（既存既定は SQLite のみ）を維持しつつ、拡張容易・テスト容易な分離設計。

## アーキテクチャ
- パイプライン: JV-Data parse → 正規化モデル → `IDatabaseWriter`（複数）へバッチ同報。
- 抽象化: 出力エンジン別の Writer/DDL/型マッピングを Strategy 化、`CompositeDatabaseWriter` で多重出力。
- 整合性: `ConsistencyMode` で `strict`/`best-effort` を切替（both 時のみ意味を持つ）。

## モジュール構成（推奨）
- 追加（最小変更案）:
  - `Urasandesu.JVLinkToSQLite/` に DuckDB 用 Writer/Schema/Mapper を実装
  - 依存: `DuckDB.NET.Data` を同プロジェクト参照に追加（x64 固定）
- 代替（分離案）:
  - 新規 `Urasandesu.JVLinkToSQLite.DuckDB/`（DuckDB 依存を隔離、将来の他 DB 拡張に有利）
- 推奨は「最小変更案」で先行実装 → 需要次第で分離にリファクタ（移行容易な境界設計）。

## 主要インターフェース
```csharp
public enum DatabaseEngine { Sqlite, DuckDb }
public enum ConsistencyMode { Strict, BestEffort }

public interface IDatabaseWriter : IDisposable
{
    Task EnsureSchemaAsync(CancellationToken ct);
    Task BeginBatchAsync(CancellationToken ct);
    Task WriteAsync(string tableName, IReadOnlyList<object[]> rows, CancellationToken ct);
    Task CommitBatchAsync(CancellationToken ct);
    Task RollbackBatchAsync(CancellationToken ct);
}

public interface ISchemaGenerator
{
    string GenerateCreateTable(TableSpec spec);
    IEnumerable<string> GenerateIndexes(TableSpec spec);
}

public interface ITypeMapper
{
    string Map(ColumnSpec column);
}

public interface IDatabaseWriterFactory
{
    IDatabaseWriter Create(DatabaseEngine engine, DbWriterOptions options);
}
```

### Composite 実装（要点）
- 複数 `IDatabaseWriter` を保持し、各メソッドを順次呼び出し。
- `strict` ならいずれか失敗で全体ロールバック、`best-effort` なら成功側継続。

## オプション/CLI
- `--db-target`: `sqlite` | `duckdb` | `both`（既定: `sqlite`）
- `--duckdb-path`: 出力先（未指定は SQLite 出力と同名で拡張子 `.duckdb`）
- `--duckdb-threads`: DuckDB 処理スレッド数（未指定は DB 既定）
- `--consistency`: `strict` | `best-effort`（`both` 時の挙動）
- 内部設定例: `DbWriterOptions { string Path; int? Threads; int BatchSize; int MaxRetry; TimeSpan BaseBackoff; }`

## DDL/型マッピング設計
- 文字列: `TEXT` → `VARCHAR`
- 整数: `INTEGER` → `INTEGER/BIGINT`（論理型の幅に追従）
- 実数: `REAL/NUMERIC` → `DOUBLE` or `DECIMAL(p,s)`（列仕様に応じ選択）
- 日時: ISO8601 `TEXT` → `TIMESTAMP`（日付のみは `DATE`）
- 真偽: `INTEGER(0/1)` → `BOOLEAN`
- バイナリ: `BLOB` → `BLOB`
- 制約/索引: 主キー/ユニーク/インデックスを両 DB で付与（DuckDB `CREATE INDEX` 対応）
- 注意: 精度差異は `DecimalPolicy` で明示（例: 金額=DECIMAL(18,2)）

## Writer 実装方針
### SqliteDatabaseWriter（既存を適応）
- 既存トランザクションとプリペアドを活用。

### DuckDbDatabaseWriter（新規）
- 依存: `DuckDB.NET.Data`
- 接続: `new DuckDBConnection($"Data Source={options.Path};")`
- スレッド: `SET threads = <n>`（必要に応じて）
- DDL: `ISchemaGenerator` から生成、起動時に `EnsureSchemaAsync`
- INSERT: プリペアド＋バルク（`BeginBatchAsync` 〜 `CommitBatchAsync`）
- リトライ: 一過性エラーに指数バックオフ（最大 3 回）

## DI/ライフサイクル
- `IDatabaseWriterFactory` を DryIoc に登録し、CLI オプションに基づき以下を生成
  - `sqlite` → `SqliteDatabaseWriter`
  - `duckdb` → `DuckDbDatabaseWriter`
  - `both` → `CompositeDatabaseWriter([Sqlite, DuckDb], consistency)`
- `ISchemaGenerator`/`ITypeMapper` は `Keyed<DatabaseEngine>` で登録し、Writer から解決。

## エラーハンドリング/ログ
- ログ追加項目: `Target`, `BatchSize`, `RowsInserted`, `DurationMs`, `Retries`, `FailedTable`, `ExceptionType`
- 例外分類: `SchemaException`, `InsertTransientException`, `InsertFatalException`
- 終了コード: `strict` で片系失敗 → 非 0。`best-effort` 片系失敗 → 0/非 0 は将来オプションで制御可。

## ビルド/配布
- プラットフォーム: x64 固定（AnyCPU 不可）
- 参照追加: `DuckDB.NET.Data`（NuGet を Restore に組込み）
- 配布物: `duckdb.dll` 等のネイティブ依存を同梱（NuGet の NativeAssets に追従）
- `Build.ps1`: まとめ表示に DuckDB 可否、x64 強制、依存検証（起動時 Fail Fast）

## 互換性
- 既定は SQLite のみで全挙動不変。
- 新オプション指定時のみ DuckDB が生成。
- 既存ログにターゲット識別子が加わる以外は互換。

## 設計上のファイル配置（案）
```
Urasandesu.JVLinkToSQLite/
  Database/
    IDatabaseWriter.cs
    IDatabaseWriterFactory.cs
    CompositeDatabaseWriter.cs
    Sqlite/
      SqliteDatabaseWriter.cs
      SqliteSchemaGenerator.cs
      SqliteTypeMapper.cs
    DuckDb/
      DuckDbDatabaseWriter.cs
      DuckDbSchemaGenerator.cs
      DuckDbTypeMapper.cs
  Options/
    DbTarget.cs
    ConsistencyMode.cs
    DbWriterOptions.cs
```

## 疑似コード（要点）
```csharp
// Factory
IDatabaseWriter Create(DatabaseEngine engine, DbWriterOptions o)
  => engine == DatabaseEngine.Sqlite
     ? new SqliteDatabaseWriter(...)
     : new DuckDbDatabaseWriter(...);

// Composite
await EnsureSchemaAsync(ct) { foreach (var w in writers) await w.EnsureSchemaAsync(ct); }
await BeginBatchAsync(ct)  { foreach (var w in writers) await w.BeginBatchAsync(ct); }
await WriteAsync(tbl, rows, ct) { foreach (var w in writers) await w.WriteAsync(tbl, rows, ct); }
await CommitBatchAsync(ct) { /* strict: 全成功のみ commit */ }
await RollbackBatchAsync(ct) { /* strict: いずれか失敗で全 rollback */ }
```

## テスト計画
- ユニット
  - 型マッピング: 境界（最大長/DECIMAL 精度/NULL/日時）
  - DDL 生成: PK/Unique/Index/Default/Nullability
  - リトライ: 一過性例外で再試行カウント・Backoff 確認
  - Composite 整合性: `strict`/`best-effort` の Commit/Rollback 順序
- 疑似結合（小規模）
  - 2 テーブルに 1000 件流し、SQLite/DuckDB の件数一致
  - 片系エラー注入時の挙動検証（`strict` 非 0、`best-effort` 継続）
- パフォーマンス目標
  - `both` で SQLite 単独比 +40% 以内（同一入力）を概算で確認

## リスク/対策
- ネイティブ DLL 読み込み失敗: x64 固定、起動時チェック、不足時の明確なガイダンス
- 精度差異: `DECIMAL` 採用を優先、要件で桁を固定、単体テストで担保
- 依存更新影響: DuckDB.NET のバージョンピン止め、リリースノート監視
- I/O ボトルネック: バッチサイズ/スレッドをオプション化、既定は安全値

## 開発フェーズ分割（マイルストーン/受入基準付き）
1. フェーズ1: 設計固定・依存導入（0.5〜1.0 日）
   - 成果: 本設計書 Fix、`DuckDB.NET.Data` 参照、x64 構成整備
   - 受入: ソリューションが Release ビルド通過（DuckDB 参照追加済）
2. フェーズ2: 抽象化導入（0.5 日）
   - 成果: `IDatabaseWriter`/`Factory`/`Composite` の骨子、既存 SQLite を `SqliteDatabaseWriter` として適用
   - 受入: SQLite 単独で従来どおりの成果物が生成
3. フェーズ3: DuckDB スキーマ/型マッピング（0.5〜1.0 日）
   - 成果: `DuckDbSchemaGenerator`/`DuckDbTypeMapper`、DDL 生成テスト
   - 受入: 単体テストで DDL/型が要件通り
4. フェーズ4: DuckDB Writer 実装（1.0〜1.5 日）
   - 成果: 接続/トランザクション/プリペアド/リトライ実装
   - 受入: 小規模データで `.duckdb` 生成・件数一致
5. フェーズ5: CLI/DI 統合（0.5 日）
   - 成果: `--db-target/--duckdb-path/--consistency`/`--duckdb-threads` 追加、DryIoc 登録
   - 受入: `sqlite/duckdb/both` の各モードが実行可能
6. フェーズ6: 同報整合性/ログ/エラー設計の仕上げ（0.5 日）
   - 成果: `strict`/`best-effort` の動作・メトリクス出力
   - 受入: 片系障害の挙動が仕様通り、終了コード検証
7. フェーズ7: テスト拡充と性能確認（0.5〜1.0 日）
   - 成果: ユニット/疑似結合、CI に最小セット登録
   - 受入: 性能目標（+40% 以内）概算クリア
8. フェーズ8: パッケージング/ドキュメント（0.5 日）
   - 成果: Build.ps1 更新、配布同梱確認、README/Usage/Wiki 更新
   - 受入: アーティファクトに DuckDB 依存含有、サンプル実行手順の再現

## 次のアクション（要確認）
- Decimal/Datetime の列ごとの精度ポリシーの確定（p,s/ISO8601↔TIMESTAMP）
- 既定出力パス（`.duckdb` 拡張子）と上書きポリシーの同一化確認
- 分離案（新プロジェクト）採用の可否（初期は最小変更で進行の提案）

