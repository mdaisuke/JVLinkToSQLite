﻿// JVLinkToSQLite は、JRA-VAN データラボが提供する競馬データを SQLite データベースに変換するツールです。
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
using System.Xml.Serialization;
using Urasandesu.JVLinkToSQLite.OperatorAggregates;

namespace Urasandesu.JVLinkToSQLite.Settings
{
    /// <summary>
    /// 動作設定詳細を表す基底クラスです。
    /// </summary>
    public abstract class JVLinkToSQLiteDetailSetting
    {
        /// <summary>
        /// 動作設定詳細が有効かどうかを取得または設定します。
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// データ種別に関する動作設定を取得または設定します。
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays")]
        public JVDataSpecSetting[] DataSpecSettings { get; set; }

        /// <summary>
        /// SQLite データベース接続情報を取得します。
        /// </summary>
        [XmlIgnore]
        public SQLiteConnectionInfo SQLiteConnectionInfo { get; private set; }

        /// <summary>
        /// DuckDB出力が有効かどうかを取得または設定します。
        /// </summary>
        public bool DuckDBEnabled { get; set; }

        /// <summary>
        /// DuckDBデータベースのファイルパスを取得または設定します。
        /// </summary>
        public string DuckDBDataSource { get; set; }

        /// <summary>
        /// DuckDBエラー時に処理を継続するかどうかを取得または設定します。
        /// </summary>
        public bool ContinueOnDuckDBError { get; set; }

        /// <summary>
        /// DuckDB データベース接続情報を取得します。
        /// </summary>
        [XmlIgnore]
        public DuckDBConnectionInfo DuckDBConnectionInfo { get; private set; }

        internal void FillWithSQLiteConnectionInfo(SQLiteConnectionInfo connInfo)
        {
            SQLiteConnectionInfo = connInfo;
        }

        internal void FillWithDuckDBConnectionInfo(DuckDBConnectionInfo connInfo)
        {
            DuckDBConnectionInfo = connInfo;
        }

        public virtual JVOperatorAggregate NewOperatorAggregate(IResolver resolver, bool isImmediate)
        {
            throw new NotImplementedException($"'{GetType()}' では未実装です。");
        }
    }
}
