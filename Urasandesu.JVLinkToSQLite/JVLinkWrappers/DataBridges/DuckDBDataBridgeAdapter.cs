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
using System.Linq;
using System.Text.RegularExpressions;
using DuckDB.NET.Data;
using Urasandesu.JVLinkToSQLite.Basis.Mixins.System.Data;

namespace Urasandesu.JVLinkToSQLite.JVLinkWrappers.DataBridges
{
    /// <summary>
    /// DuckDB用のDataBridgeアダプタ実装
    /// </summary>
    public class DuckDBDataBridgeAdapter : IDataBridgeAdapter
    {
        public IEnumerable<IPreparedCommand> BuildUpCreateTableCommand(IPreparedCommandCache commandCache, DataBridge dataBridge)
        {
            // SQLiteDataBridgeAdapterと同様の実装で、CREATE TABLE文を変換
            if (commandCache is DuckDBPreparedCommandCache duckdbCache)
            {
                var sqliteCommands = dataBridge.BuildUpCreateTableCommand(new SQLitePreparedCommandCacheDummy());
                foreach (var sqliteCmd in sqliteCommands)
                {
                    var duckdbSql = ConvertSQLiteToDuckDBCreateTable(sqliteCmd.GetLoggingQuery());
                    yield return commandCache.Get(duckdbSql);
                }
            }
            else
            {
                throw new InvalidOperationException("DuckDBDataBridgeAdapter requires DuckDBPreparedCommandCache");
            }
        }

        public IEnumerable<IPreparedCommand> BuildUpInsertCommand(IPreparedCommandCache commandCache, DataBridge dataBridge)
        {
            if (commandCache is DuckDBPreparedCommandCache duckdbCache)
            {
                var sqliteCommands = dataBridge.BuildUpInsertCommand(new SQLitePreparedCommandCacheDummy());
                foreach (var sqliteCmd in sqliteCommands)
                {
                    // INSERT文はそのまま使用可能
                    yield return commandCache.Get(sqliteCmd.GetLoggingQuery());
                }
            }
            else
            {
                throw new InvalidOperationException("DuckDBDataBridgeAdapter requires DuckDBPreparedCommandCache");
            }
        }

        /// <summary>
        /// SQLiteのCREATE TABLE文をDuckDB用に変換します。
        /// </summary>
        private string ConvertSQLiteToDuckDBCreateTable(string sqliteCreateTable)
        {
            var sql = sqliteCreateTable;

            // IF NOT EXISTS の処理（DuckDBでもサポート）
            // PRIMARY KEY AUTOINCREMENT を GENERATED ALWAYS AS IDENTITY に変換
            sql = Regex.Replace(sql, @"INTEGER\s+PRIMARY\s+KEY\s+AUTOINCREMENT", 
                "BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY", RegexOptions.IgnoreCase);

            // TEXT型はVARCHARに変換（DuckDBはTEXTもサポートするが、VARCHARが推奨）
            sql = Regex.Replace(sql, @"\bTEXT\b", "VARCHAR", RegexOptions.IgnoreCase);

            // REAL型はDOUBLEに変換
            sql = Regex.Replace(sql, @"\bREAL\b", "DOUBLE", RegexOptions.IgnoreCase);

            // DATETIME型はTIMESTAMPに変換
            sql = Regex.Replace(sql, @"\bDATETIME\b", "TIMESTAMP", RegexOptions.IgnoreCase);

            return sql;
        }

        /// <summary>
        /// SQLitePreparedCommandCacheのダミー実装
        /// </summary>
        private class SQLitePreparedCommandCacheDummy : Urasandesu.JVLinkToSQLite.Basis.Mixins.System.Data.SQLitePreparedCommandCache
        {
            public SQLitePreparedCommandCacheDummy() : base(null)
            {
            }

            public override SQLitePreparedCommand Get(string key)
            {
                // ダミーコマンドを返す
                return new SQLitePreparedCommandDummy(key);
            }
        }

        /// <summary>
        /// SQLitePreparedCommandのダミー実装
        /// </summary>
        private class SQLitePreparedCommandDummy : Urasandesu.JVLinkToSQLite.Basis.Mixins.System.Data.SQLitePreparedCommand
        {
            private readonly string _commandText;

            public SQLitePreparedCommandDummy(string commandText) : base(null, null)
            {
                _commandText = commandText;
            }

            public override string GetLoggingQuery()
            {
                return _commandText;
            }
        }
    }
}