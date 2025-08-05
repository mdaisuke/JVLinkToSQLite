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

using System;
using System.Collections.Generic;
using System.Data.Common;

namespace Urasandesu.JVLinkToSQLite.Basis.Mixins.System.Data
{
    /// <summary>
    /// DuckDB用の準備済みコマンドキャッシュを表すクラス
    /// </summary>
    public class DuckDBPreparedCommandCache : IPreparedCommandCache
    {
        private readonly DbConnection _connection;
        private DbTransaction _transaction;
        private readonly Dictionary<string, DuckDBPreparedCommand> _cache = new Dictionary<string, DuckDBPreparedCommand>();
        private bool _disposed;

        public DuckDBPreparedCommandCache(DbConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _transaction = _connection.BeginTransaction();
        }

        public IPreparedCommand Get(string commandText)
        {
            if (_cache.TryGetValue(commandText, out var cachedCommand))
            {
                // 既存のコマンドのパラメータをクリア
                cachedCommand.Command.Parameters.Clear();
                return cachedCommand;
            }

            var command = _connection.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = commandText;
            
            var preparedCommand = new DuckDBPreparedCommand(command);
            _cache[commandText] = preparedCommand;
            
            return preparedCommand;
        }

        public void Commit()
        {
            _transaction?.Commit();
            _transaction?.Dispose();
            _transaction = null;
        }

        public void CommitAndNewTransaction()
        {
            Commit();
            _transaction = _connection.BeginTransaction();
            
            // 既存のコマンドのトランザクションを更新
            foreach (var cmd in _cache.Values)
            {
                cmd.Command.Transaction = _transaction;
            }
        }

        public void Rollback()
        {
            _transaction?.Rollback();
            _transaction?.Dispose();
            _transaction = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (var cmd in _cache.Values)
                    {
                        cmd.Dispose();
                    }
                    _cache.Clear();
                    
                    _transaction?.Dispose();
                    _connection?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}