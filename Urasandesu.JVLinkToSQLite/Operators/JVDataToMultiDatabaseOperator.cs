// JVLinkToSQLite は、JRA-VAN データラボが提供する競馬データを SQLite データベースに変換するツールです。
// 
// Copyright (C) 2023 Akira Sugiura
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
// 
// Additional permission under GNU GPL version 3 section 7
// 
// If you modify this Program, or any covered work, by linking or combining it with 
// ObscUra (or a modified version of that library), containing parts covered 
// by the terms of ObscUra's license, the licensors of this Program grant you 
// additional permission to convey the resulting work.

using DryIoc;
using System;
using System.Threading.Tasks;
using Urasandesu.JVLinkToSQLite.JVLinkWrappers;
using Urasandesu.JVLinkToSQLite.Settings;
using static Urasandesu.JVLinkToSQLite.JVOperationMessenger;

namespace Urasandesu.JVLinkToSQLite.Operators
{
    /// <summary>
    /// 複数のデータベースに対して並列にデータを出力するオペレーター
    /// </summary>
    internal class JVDataToMultiDatabaseOperator : IJVDataToDatabaseOperator
    {
        public class Factory
        {
            private readonly IResolver _resolver;
            private readonly IJVServiceOperationListener _listener;

            public Factory(IResolver resolver, IJVServiceOperationListener listener)
            {
                _resolver = resolver;
                _listener = listener;
            }

            public virtual IJVDataToDatabaseOperator New(
                SQLiteConnectionInfo sqliteConnInfo,
                DuckDBConnectionInfo duckdbConnInfo,
                JVOpenResult openRslt,
                JVRecordSpec[] excludedRecordSpecs,
                bool continueOnDuckDBError)
            {
                var sqliteOperator = _resolver.Resolve<JVDataToSQLiteOperator.Factory>()
                    .New(sqliteConnInfo, openRslt, excludedRecordSpecs);
                var duckdbOperator = _resolver.Resolve<JVDataToDuckDBOperator.Factory>()
                    .New(duckdbConnInfo, openRslt, excludedRecordSpecs);

                return new JVDataToMultiDatabaseOperator(
                    _listener,
                    sqliteOperator,
                    duckdbOperator,
                    continueOnDuckDBError);
            }
        }

        private readonly IJVServiceOperationListener _listener;
        private readonly IJVDataToDatabaseOperator _sqliteOperator;
        private readonly IJVDataToDatabaseOperator _duckdbOperator;
        private readonly bool _continueOnDuckDBError;

        public JVDataToMultiDatabaseOperator(
            IJVServiceOperationListener listener,
            IJVDataToDatabaseOperator sqliteOperator,
            IJVDataToDatabaseOperator duckdbOperator,
            bool continueOnDuckDBError)
        {
            _listener = listener;
            _sqliteOperator = sqliteOperator;
            _duckdbOperator = duckdbOperator;
            _continueOnDuckDBError = continueOnDuckDBError;
        }

        public JVLinkServiceOperationResult InsertOrUpdateAll()
        {
            Info(_listener, this, args => $"SQLiteとDuckDBへの並列出力を開始します。");

            var sqliteTask = Task.Run(() => _sqliteOperator.InsertOrUpdateAll());
            var duckdbTask = Task.Run(() =>
            {
                try
                {
                    return _duckdbOperator.InsertOrUpdateAll();
                }
                catch (Exception ex)
                {
                    if (_continueOnDuckDBError)
                    {
                        Warning(_listener, this, args => $"DuckDBへの出力でエラーが発生しましたが、処理を継続します。エラー: {args[0]}", ex.Message);
                        return JVLinkServiceOperationResult.Success(nameof(InsertOrUpdateAll));
                    }
                    throw;
                }
            });

            // 両方のタスクが完了するまで待機
            Task.WaitAll(sqliteTask, duckdbTask);

            var sqliteResult = sqliteTask.Result;
            var duckdbResult = duckdbTask.Result;

            // SQLiteが失敗した場合は、その結果を返す
            if (!sqliteResult.IsSuccess)
            {
                Warning(_listener, this, args => $"SQLiteへの出力でエラーが発生しました。");
                return sqliteResult;
            }

            // DuckDBが失敗した場合
            if (!duckdbResult.IsSuccess && !_continueOnDuckDBError)
            {
                Warning(_listener, this, args => $"DuckDBへの出力でエラーが発生しました。");
                return duckdbResult;
            }

            Info(_listener, this, args => $"SQLiteとDuckDBへの並列出力が完了しました。");
            return JVLinkServiceOperationResult.Success(nameof(InsertOrUpdateAll));
        }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _sqliteOperator?.Dispose();
                    _duckdbOperator?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}