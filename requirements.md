# DuckDB 同時出力対応 要件定義書

## 目的
- SQLite 出力に加え、同一処理フローで DuckDB 形式（単一 `.duckdb` ファイル）への同時出力を実現する。
- 既存の CLI・バッチ運用・パッケージングに後方互換で追加し、性能と保守性を両立する。

## スコープ
- 同期バッチ処理での「SQLite + DuckDB」同時出力。
- 既存スキーマ（論理）を DuckDB に最適化した物理型へマッピング。
- 新規 CLI オプション、DI 登録、DDL 生成、INSERT 実装、進捗・統計ログ、ビルド/配布変更。
- NUnit テスト（ユニット/簡易結合）とドキュメント更新。

## 非スコープ
- 既存 SQLite 仕様の破壊的変更。
- DuckDB へのストリーミング/増分ロード・外部テーブル連携・並列読取提供。
- 既存 JV-Link 依存部の機能追加・変更。

## 現状整理
- 現行は SQLite 単独出力。JV-Data のパース → 正規化 → DDL/INSERT → SQLite ファイル出力。
- DI は DryIoc。Ado.NET/ラッパーを介した DB 書き込み抽象が存在（想定）。

## 全体像（アーキテクチャ）
- 取り込みパイプラインを「1 回のパース → 2 つのシンク（SQLite/DuckDB）」に拡張。
- 既存 `ISqliteWriter` 相当を一般化した `IDatabaseWriter`（または `IDbSink`）を新設/拡張。
- `CompositeWriter` で選択された複数ターゲットへバッチ単位に同報。
- DDL/型マッピングはエンジン別テンプレートを注入（Strategy）。
- 失敗時の整合性ポリシーを切替（all-or-nothing / best-effort）。

## 機能要件
- 出力ターゲット選択:
  - SQLite のみ（デフォルト互換）
  - DuckDB のみ
  - 両方（同時出力）
- 出力先:
  - SQLite: 既存踏襲（既定パス/明示パス）
  - DuckDB: 既定は SQLite と同ディレクトリ、同名で拡張子 `.duckdb`（上書き挙動は SQLite に準拠）
- スキーマ:
  - 論理スキーマは共通。DuckDB は物理型を最適化（下記）。
- トランザクション:
  - バッチ単位のトランザクションを各 DB で実施。
  - 同報時、片系失敗の扱いはモードで制御（下記）。
- ログ/メトリクス:
  - 開始/終了、対象 DB、経過時間、挿入件数、失敗件数、失敗テーブル、再試行回数。
- 失敗時ポリシー:
  - 厳格: いずれかの DB でバッチ失敗 → 両方ロールバック → 処理中断（既定 for both）。
  - ベストエフォート: 失敗 DB は中断し、成功側のみ続行。終了コードは警告（オプションで選択）。
- 進捗表示/終了コード:
  - ターゲットごとの成功/失敗を要約。厳格モードでの失敗時は非 0 終了。

## CLI/オプション仕様（提案）
- `--db-target`: `sqlite` | `duckdb` | `both`（デフォルト: `sqlite`）
- `--duckdb-path`: 出力ファイルパス（省略時は SQLite の出力と同名で `.duckdb`）
- `--duckdb-threads`: 挿入処理のスレッド数/並列度（省略時は自動）
- `--consistency`: `strict` | `best-effort`（`both`時の整合性。デフォルト `strict`）
- 既存オプション/ヘルプに統合。未指定時は完全後方互換。

## データ仕様（DDL/型マッピング方針）
- 文字列: SQLite `TEXT` → DuckDB `VARCHAR`
- 数値（整数）: SQLite `INTEGER` → DuckDB `INTEGER/BIGINT`（サイズは現行仕様に追従）
- 数値（小数）: SQLite `NUMERIC`/`REAL` → DuckDB `DECIMAL(p,s)` もしくは `DOUBLE`（列の範囲に応じて選択）
- 日時: SQLite `TEXT`(ISO8601) → DuckDB `TIMESTAMP`（または `DATE`/`TIME`）
- 真偽: SQLite `INTEGER(0/1)` → DuckDB `BOOLEAN`
- バイナリ: SQLite `BLOB` → DuckDB `BLOB`
- 主キー/ユニーク/インデックス: 可能な限り DDL で同等制約を付与（DuckDB は CREATE INDEX 対応）。
- NULL 許容/既定値: スキーマ定義に合わせ両 DB で統一。
- 文字コード: UTF-8（DuckDB/SQLite とも）。

## エラーハンドリング/整合性
- DDL 段階失敗: 直ちに対象 DB を中断。`strict` では全体中断。
- INSERT 段階失敗: バッチロールバック。`strict` では他 DB も中断。
- 再試行: 一時的失敗（ロック/一過性）には指数バックオフで最大 N 回（N=3 想定）。
- 出力ファイルの上書き: 既定は再作成（オプションで追記/再利用を将来検討、現段階は非対応）。

## 性能/リソース
- 目標: 同時出力（both）で SQLite 単独比 +40% 以内の増加に抑制（同一入力・同一ハード）。
- 手段:
  - 1 回のパース結果を共有（ダブルパース禁止）
  - 両 DB ともプリペアドステートメント + バルクサイズ可変（例: 500〜2,000 行/バッチ）
  - I/O はシーケンシャル優先。DuckDB は `SET threads` やメモリ制限を適用可能に
- メモリ: 常時使用量は既存比 +200MB 以内（大規模投入時）。

## 互換性/移行
- 既存の呼び出しは無変更で SQLite のみ出力。
- 新オプション利用時のみ DuckDB が生成。
- 既存成果物やログ形式は保持（ログにターゲット列を追加）。

## セキュリティ/配布
- ADO.NET プロバイダー: `DuckDB.NET.Data`（.NET Framework 4.8 対応版を採用）
- ネイティブ依存: Windows x64 `duckdb.dll` を NuGet から取り込み、配布物に同梱。
- 対応アーキテクチャ: x64 固定（x86 は非対応）。ビルド構成は x64 へ統一。
- ライセンス確認: DuckDB/DuckDB.NET のライセンス表記を配布物へ同梱（必要に応じて NOTICE）。
- シークレットなし。既存の鍵運用（`Build.ps1` 引数）は変更なし。

## ビルド/パッケージング変更
- 参照追加: `Urasandesu.JVLinkToSQLite`（または新規 `Urasandesu.JVLinkToSQLite.DuckDB`）に `DuckDB.NET.Data`。
- プラットフォーム: AnyCPU → x64（ネイティブ DLL 読み込み安定化）。
- 配布物: `work/JVLinkToSQLiteArtifact_*.exe` に DuckDB 依存バイナリを同梱。
- Build.ps1: 依存解決・x64 固定・サマリ出力に DuckDB 可否を追記。

## 実装方針（モジュール/DI）
- インターフェース:
  - `IDatabaseWriter`（既存の SQLite 実装を内包/置換）
  - `ISchemaGenerator`（エンジン別 DDL）
  - `ITypeMapper`（論理 → 物理型）
- 実装:
  - `SQLiteWriter`（既存流用）
  - `DuckDBWriter`（新規。接続文字列は `Data Source=<path>;` 形式）
  - `CompositeWriter`（ターゲット複数を束ねる）
- DI 登録:
  - `--db-target` に応じて `IDatabaseWriter` へ単/複合を登録（DryIoc の Keyed/条件付き登録を利用）
- プロジェクト構成（案）:
  - 既存 `Urasandesu.JVLinkToSQLite` に Writer/Schema/TypeMapper の DuckDB 実装を追加
  - もしくは新規 `Urasandesu.JVLinkToSQLite.DuckDB` プロジェクトを追加し依存分離（将来拡張性重視）

## テスト要件
- ユニット:
  - 型マッピング（境界値・NULL・日時・小数精度）
  - DDL 生成（主キー/インデックス/既定値）
  - バッチ/トランザクション制御（コミット/ロールバック）
- 簡易結合（オプトイン）:
  - 小規模サンプルで SQLite/DuckDB 両方に投入 → 件数一致/スキーマ整合。
- 実行例（CLI）:
  - `--db-target both --duckdb-path <out.duckdb>` で 2 ファイル生成、終了コード 0。

## 受け入れ基準
- `--db-target sqlite` で従来と同一成果物・同一件数・同一ログ形式（ターゲット表記差分のみ）。
- `--db-target duckdb` で `.duckdb` が生成され、テーブル/インデックス/件数が仕様通り。
- `--db-target both` で 2 ファイル生成、件数一致、`strict` で片系失敗時に非 0 終了。
- 性能指標・メモリ上限を満たす（上記性能/リソース）。
- Build.ps1 で Release パッケージに DuckDB 依存が含まれる。

## ログ/監視
- 追加フィールド: Target, BatchSize, RowsInserted, DurationMs, Failures。
- 失敗時: 例外種別・SQL・先頭数件のパラメータ値をマスクして出力。

## リスクと対応
- ネイティブ DLL ロード失敗: x64 固定・同梱・起動時チェックで早期 Fail Fast。
- 型差異/精度ズレ: 明示的 DECIMAL 採用と単体テストで検知。
- パフォーマンス劣化: バッチサイズ/並列度をオプション化、デフォルトは安全値。
- 依存更新影響: DuckDB.NET のバージョンをピン止め。リリースノート追従。

## スケジュール（目安）
- 設計/雛形/依存導入: 1.0 日
- Writer/DDL/Mapper 実装: 2.0 日
- CLI/DI/ログ統合: 1.0 日
- テスト/調整: 1.5 日
- パッケージング/ドキュメント: 0.5 日
- 合計: 約 6 営業日

## ドキュメント/変更
- README/Usage: オプション追記、サンプル実行例。
- 変更履歴: 機能追加（後方互換）、x64 要件明記。
- Wiki: アーキ概要図・FAQ（DLL ロード/strict と best-effort の違い）。

## 未確定事項（要確認）
- DuckDB.NET のバージョンとサポート対象 OS（Windows x64 前提で問題ないか）。
- 既存の出力既定パス命名規則に合わせた DuckDB の既定名（拡張子のみ違いで良いか）。
- 小数/日時の既存仕様（CSV/固定長起源）に対する最適精度（DECIMAL 桁の確定）。
- `best-effort` の既定可否（現提案は `strict` 既定）。

