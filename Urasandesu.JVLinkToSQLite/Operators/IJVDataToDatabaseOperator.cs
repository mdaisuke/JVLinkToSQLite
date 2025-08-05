using System;

namespace Urasandesu.JVLinkToSQLite.Operators
{
    /// <summary>
    /// JRA-VANデータをデータベースに格納するオペレーターのインターフェース
    /// </summary>
    public interface IJVDataToDatabaseOperator : IDisposable
    {
        /// <summary>
        /// 全データの挿入または更新を実行します。
        /// </summary>
        /// <returns>処理結果</returns>
        JVLinkServiceOperationResult InsertOrUpdateAll();
    }
}