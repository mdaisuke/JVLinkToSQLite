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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DuckDB.NET.Data;
using Urasandesu.JVLinkToSQLite.Basis.Mixins.System.Data;
using Urasandesu.JVLinkToSQLite.Operators;

namespace Urasandesu.JVLinkToSQLite.JVLinkWrappers.DataBridges
{
    /// <summary>
    /// DuckDB用のDataBridgeアダプタ実装
    /// </summary>
    public class DuckDBDataBridgeAdapter : IDataBridgeAdapter
    {
        public IEnumerable<IPreparedCommand> BuildUpCreateTableCommand(IPreparedCommandCache commandCache, DataBridge dataBridge)
        {
            // メインテーブルのCREATE TABLE文を生成
            var createTableSql = ConvertSQLiteToDuckDBCreateTable(dataBridge.Columns.GetCommandText(dataBridge.TableName));
            var command = commandCache.Get(createTableSql);
            yield return command;

            // 子テーブルのCREATE TABLE文を生成
            if (dataBridge.ChildTableNameList != null)
            {
                for (var i = 0; i < dataBridge.ChildTableNameList.Count; i++)
                {
                    var childTableName = dataBridge.ChildTableNameList[i];
                    var childCreateTableSql = ConvertSQLiteToDuckDBCreateTable(
                        dataBridge.ChildCreateTableSourcesList[i].GetCommandText(childTableName));
                    var childCommand = commandCache.Get(childCreateTableSql);
                    yield return childCommand;
                }
            }
        }

        public IEnumerable<IPreparedCommand> BuildUpInsertCommand(IPreparedCommandCache commandCache, DataBridge dataBridge)
        {
            // SQLite用のDataBridgeメソッドはSQLitePreparedCommandCacheに依存しているため、
            // DuckDBでは独自の実装が必要
            
            // メインテーブルのINSERT文を生成
            var columns = dataBridge.Columns.Value;
            var columnNames = new List<string>();
            var paramNames = new List<string>();
            
            foreach (var column in columns.Where(c => !c.IsId))
            {
                columnNames.Add(column.ColumnName);
                paramNames.Add("$" + column.ParameterName);
            }
            
            var insertSql = $"INSERT INTO {dataBridge.TableName} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", paramNames)})";
            var command = commandCache.Get(insertSql);
            
            // パラメータ値を設定
            var baseGetter = dataBridge.BaseGetter;
            foreach (var column in columns.Where(c => !c.IsId))
            {
                var value = baseGetter(column.ColumnName);
                if (command is DuckDBPreparedCommand duckdbCmd && duckdbCmd.Command is DuckDBCommand duckdbCommand)
                {
                    var param = duckdbCommand.CreateParameter();
                    param.ParameterName = "$" + column.ParameterName;
                    param.Value = value ?? DBNull.Value;
                    duckdbCommand.Parameters.Add(param);
                }
            }
            
            yield return command;

            // 子テーブルのINSERT文を生成
            if (dataBridge.ChildTableNameList != null)
            {
                for (var i = 0; i < dataBridge.ChildTableNameList.Count; i++)
                {
                    var childTableName = dataBridge.ChildTableNameList[i];
                    var childRowCount = dataBridge.ChildRowCountList[i];
                    var childRowMasks = dataBridge.ChildRowMasksList[i];
                    var childGetter = dataBridge.ChildGetterList[i];
                    var childPureColumns = dataBridge.ChildPureColumnsList[i];

                    for (var j = 0; j < childRowCount; j++)
                    {
                        if (childRowMasks[j])
                        {
                            var childColumnNames = new List<string>();
                            var childParamNames = new List<string>();
                            
                            foreach (var column in childPureColumns.Value.Where(c => !c.IsId))
                            {
                                childColumnNames.Add(column.ColumnName);
                                childParamNames.Add("$" + column.ParameterName + "_" + j);
                            }
                            
                            var childInsertSql = $"INSERT INTO {childTableName} ({string.Join(", ", childColumnNames)}) VALUES ({string.Join(", ", childParamNames)})";
                            var childCommand = commandCache.Get(childInsertSql);
                            
                            // パラメータ値を設定
                            foreach (var column in childPureColumns.Value.Where(c => !c.IsId))
                            {
                                var value = childGetter(dataBridge.Prefix + j + column.ColumnName);
                                if (childCommand is DuckDBPreparedCommand duckdbCmd && duckdbCmd.Command is DuckDBCommand duckdbCommand)
                                {
                                    var param = duckdbCommand.CreateParameter();
                                    param.ParameterName = "$" + column.ParameterName + "_" + j;
                                    param.Value = value ?? DBNull.Value;
                                    duckdbCommand.Parameters.Add(param);
                                }
                            }
                            
                            yield return childCommand;
                        }
                    }
                }
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
    }
}