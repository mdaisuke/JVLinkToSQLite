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
using DuckDB.NET.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Urasandesu.JVLinkToSQLite.Basis.Mixins.System;
using Urasandesu.JVLinkToSQLite.Basis.Mixins.System.Collections.Concurrent;
using Urasandesu.JVLinkToSQLite.Basis.Mixins.System.Data;
using Urasandesu.JVLinkToSQLite.JVLinkWrappers;
using Urasandesu.JVLinkToSQLite.JVLinkWrappers.DataBridges;
using Urasandesu.JVLinkToSQLite.Settings;
using static Urasandesu.JVLinkToSQLite.JVOperationMessenger;

namespace Urasandesu.JVLinkToSQLite.Operators
{
    internal class JVDataToDuckDBOperator : IJVDataToDatabaseOperator
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

            public virtual IJVDataToDatabaseOperator New(DuckDBConnectionInfo connInfo, JVOpenResult openRslt, JVRecordSpec[] excludedRecordSpecs)
            {
                return new JVDataToDuckDBOperator(_resolver, _listener, connInfo, openRslt, excludedRecordSpecs);
            }
        }

        private readonly IResolver _resolver;
        private readonly IJVServiceOperationListener _listener;
        private readonly DuckDBConnection _conn;
        private readonly int _throttleSize;
        private readonly JVOpenResult _openRslt;
        private readonly JVRecordSpec[] _excludedRecordSpecs;

        public JVDataToDuckDBOperator(IResolver resolver,
                                      IJVServiceOperationListener listener,
                                      DuckDBConnectionInfo connInfo,
                                      JVOpenResult openRslt,
                                      JVRecordSpec[] excludedRecordSpecs)
        {
            _resolver = resolver;
            _listener = listener;

            var connStr = new DuckDBConnectionStringBuilder();
            connStr.DataSource = connInfo.DataSource;
            _conn = new DuckDBConnection(connStr.ToString());
            _throttleSize = connInfo.ThrottleSize;
            _openRslt = openRslt;
            _excludedRecordSpecs = excludedRecordSpecs;
        }

        public virtual JVLinkServiceOperationResult InsertOrUpdateAll()
        {
            _conn.Open();
            Command = _conn.CreateCommand();
            var commandCache = new DuckDBPreparedCommandCache(Command, _conn.BeginTransaction());
            try
            {
                // DuckDBでは独自のハンドラーを使用
                var reader = _resolver.Resolve<JVOpenResultReader.Factory>().New(_openRslt, null);
                using (var bgDuckDBWkr = new BackgroundDuckDBWorker(this, commandCache))
                {
                    bgDuckDBWkr.Start();
                    foreach (var readRslt in reader)
                    {
                        bgDuckDBWkr.Enqueue(readRslt);
                    }
                    bgDuckDBWkr.Join();
                }
                return JVLinkServiceOperationResult.Success(nameof(InsertOrUpdateAll));
            }
            catch (JVLinkException ex)
            {
                commandCache.Rollback();
                var oprRslt = JVLinkServiceOperationResult.From(ex.JVLinkResult);
                Warning(_listener,
                        this,
                        args => $"エラーが発生しました。エラー (引数)：{args[0]} ({StringMixin.JoinIfAvailable(", ", args[1])})",
                        oprRslt.DebugMessage,
                        oprRslt.Arguments);
                return oprRslt;
            }
            catch
            {
                commandCache.Rollback();
                throw;
            }
        }

        protected virtual void CreateTable(DuckDBPreparedCommandCache commandCache, DataBridge dataBridge)
        {
            var stopwatch = Stopwatch.StartNew();
            DebugStart(_listener, this, args => $"DuckDB CreateTable．．．");
            var adapter = new DuckDBDataBridgeAdapter();
            foreach (var builtCommand in adapter.BuildUpCreateTableCommand(commandCache, dataBridge))
            {
                Verbose(_listener, this, args => $"テーブル作成：{args[0]}", builtCommand.GetLoggingQuery());
                builtCommand.ExecuteNonQuery();
            }
            DebugEnd(_listener, this, args => $"DuckDB CreateTable．．． {args[0]}ms", stopwatch.ElapsedMilliseconds);
        }

        protected virtual void Insert(DuckDBPreparedCommandCache commandCache, DataBridge dataBridge)
        {
            var stopwatch2 = Stopwatch.StartNew();
            DebugStart(_listener, this, args => $"DuckDB Insert．．．");
            var adapter = new DuckDBDataBridgeAdapter();
            foreach (var builtCommand in adapter.BuildUpInsertCommand(commandCache, dataBridge))
            {
                Verbose(_listener, this, args => $"レコード作成：{args[0]}", builtCommand.GetLoggingQuery());
                builtCommand.ExecuteNonQuery();
            }
            DebugEnd(_listener, this, args => $"DuckDB Insert．．． {args[0]}ms", stopwatch2.ElapsedMilliseconds);
        }

        public IDbCommand Command { get; private set; }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _conn.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private class BackgroundDuckDBWorker : IDisposable
        {
            private readonly JVDataToDuckDBOperator _this;
            private readonly DuckDBPreparedCommandCache _commandCache;
            private readonly HashSet<string> _publishedDdlSet = new HashSet<string>();
            private readonly AsyncAccumulatingQueue<JVReadResult> _queue;
            private Thread _thread;
            private Exception _threadEx;

            public BackgroundDuckDBWorker(JVDataToDuckDBOperator @this, DuckDBPreparedCommandCache commandCache)
            {
                _this = @this;
                _queue = new AsyncAccumulatingQueue<JVReadResult>(@this._throttleSize);
                _commandCache = commandCache;
            }

            public void Start()
            {
                _thread = new Thread(() =>
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        var infoElapsed = stopwatch.Elapsed;
                        var recordCount = 0;
                        foreach (var readRslt in _queue)
                        {
                            if (readRslt.Status == JVReadStatus.RecordsExist)
                            {
                                var dataBridgeFactory = readRslt.GetDataBridgeFactory(_this._resolver);
                                var dataBridge = dataBridgeFactory.NewDataBridge();

                                if (!_publishedDdlSet.Contains(dataBridge.TableName))
                                {
                                    InfoStart(_this._listener, this, args => $"DuckDB 更新．．．テーブル：{args[0]}", dataBridge.TableName);

                                    _this.CreateTable(_commandCache, dataBridge);

                                    _publishedDdlSet.Add(dataBridge.TableName);
                                }

                                recordCount++;
                                if (stopwatch.Elapsed - infoElapsed > TimeSpan.FromSeconds(1))
                                {
                                    Info(_this._listener, this, args => $"DuckDB 更新中．．．{args[0]} レコード", recordCount);
                                    infoElapsed = stopwatch.Elapsed;
                                }
                                _this.Insert(_commandCache, dataBridge);
                            }
                            else if (readRslt.Status == JVReadStatus.FileChanged)
                            {
                                Info(_this._listener, this, args => $"DuckDB 更新完了（ファイル '{args[0]}' 分）．．． {args[1]} レコード", readRslt.FileName, recordCount);
                                stopwatch = Stopwatch.StartNew();
                                infoElapsed = stopwatch.Elapsed;
                                recordCount = 0;
                                _commandCache.CommitAndNewTransaction();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _threadEx = ex;
                    }
                });
                _thread.Start();
            }

            public void Enqueue(JVReadResult readRslt)
            {
                _queue.Enqueue(readRslt, _ => _.Status == JVReadStatus.FileChanged);
            }

            public void Join()
            {
                _queue.Join();
                _thread.Join();
                if (_threadEx != null)
                {
                    ExceptionDispatchInfo.Capture(_threadEx).Throw();
                }
                _commandCache.Commit();
                InfoEnd(_this._listener, this, args => $"DuckDB 更新．．．テーブル：{StringMixin.JoinIfAvailable(", ", args[0])}", _publishedDdlSet);
            }

            private bool disposedValue;
            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        _queue.Dispose();
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

    /// <summary>
    /// DuckDB用のPreparedCommandCache実装
    /// </summary>
    internal class DuckDBPreparedCommandCache : IPreparedCommandCache
    {
        private readonly IDbCommand _command;
        private readonly IDbTransaction _transaction;
        private readonly Dictionary<string, DuckDBPreparedCommand> _cache = new Dictionary<string, DuckDBPreparedCommand>();

        public DuckDBPreparedCommandCache(IDbCommand command, IDbTransaction transaction)
        {
            _command = command;
            _transaction = transaction;
        }

        public IPreparedCommand Get(string commandText)
        {
            if (!_cache.TryGetValue(commandText, out var preparedCommand))
            {
                var newCommand = _command.Connection.CreateCommand();
                newCommand.CommandText = commandText;
                newCommand.Transaction = _transaction;
                preparedCommand = new DuckDBPreparedCommand(newCommand);
                _cache[commandText] = preparedCommand;
            }
            return preparedCommand;
        }

        public void Commit()
        {
            _transaction.Commit();
        }

        public void CommitAndNewTransaction()
        {
            _transaction.Commit();
            var newTransaction = _command.Connection.BeginTransaction();
            foreach (var preparedCommand in _cache.Values)
            {
                preparedCommand.Command.Transaction = newTransaction;
            }
        }

        public void Rollback()
        {
            _transaction.Rollback();
        }

        public void Dispose()
        {
            foreach (var preparedCommand in _cache.Values)
            {
                preparedCommand.Dispose();
            }
            _transaction.Dispose();
        }
    }

    /// <summary>
    /// DuckDB用のPreparedCommand実装
    /// </summary>
    internal class DuckDBPreparedCommand : IPreparedCommand
    {
        public IDbCommand Command { get; }

        public DuckDBPreparedCommand(IDbCommand command)
        {
            Command = command;
        }

        public void ExecuteNonQuery()
        {
            Command.ExecuteNonQuery();
        }

        public string GetLoggingQuery()
        {
            return Command.CommandText;
        }

        public void Dispose()
        {
            Command.Dispose();
        }
    }
}