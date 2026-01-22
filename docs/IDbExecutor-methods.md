# IDbExecutor Method Matrix

| Method | Return | Streaming | SQL Server | PostgreSQL | Oracle | Internal Tech | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| ExecuteReaderAsync | DbDataReader | ✅ single | ✅ | ✅ | ❌ | ExecuteReaderAsync | Caller owns reader; lowest memory; Oracle not allowed. |
| StreamAsync | IAsyncEnumerable\<IDataRecord\> | ✅ single | ✅ | ✅ | ❌ | ExecuteReaderAsync + ReadAsync | No buffering/multi-set; Oracle fails fast. |
| ExecuteScalarAsync\<T\> | T | N/A | ✅ | ✅ | ✅ | ExecuteScalarAsync | Supports output parameters. |
| QueryTableAsync | DataTable | ❌ | ✅ | ✅ | ✅ | Provider DataAdapter.Fill(DataTable) | SQL/PG single SELECT buffered; Oracle/PG refcursor handled via adapter; output parameters available via DataTable.ExtendedProperties["OutputParameters"]. |
| QueryAsync\<T\> | List\<T\> | ❌ | ✅ | ✅ | ✅ | QueryTableAsync + DataTable→List | Caller supplies Func\<DataRow, T\>; buffered. |
| ExecuteDataSetAsync | DataSet | ❌ | ✅ | ✅ | ✅ | Provider DataAdapter.Fill(DataSet) | SQL multi-SELECT; Oracle REF CURSOR; PG refcursor/multi-SELECT; output parameters available via `DataSet.ExtendedProperties["OutputParameters"]`. |

## Performance Guide
- Streaming (SQL Server/PG): ExecuteReaderAsync / StreamAsync — fastest, lowest memory, single result only.
- Buffered single: QueryTableAsync / QueryAsync — higher memory, stable, supports output params and cursor-based results.
- Buffered multi: ExecuteDataSetAsync — highest memory; required for multiple result sets and Oracle/PG cursor scenarios.
