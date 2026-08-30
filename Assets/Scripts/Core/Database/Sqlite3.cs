using System;
using System.Runtime.InteropServices;
using System.Text;

public sealed class SqliteException : Exception
{
    public int ResultCode { get; }

    public SqliteException(int resultCode, string message)
        : base($"SQLite error {resultCode}: {message}")
    {
        ResultCode = resultCode;
    }
}

internal static class Sqlite3Native
{
    private const string LibraryName = "sqlite3";

    internal const int Ok = 0;
    internal const int Row = 100;
    internal const int Done = 101;

    internal const int OpenReadWrite = 0x00000002;
    internal const int OpenCreate = 0x00000004;
    internal const int OpenFullMutex = 0x00010000;

    private static readonly IntPtr Transient = new IntPtr(-1);

    [DllImport(LibraryName, EntryPoint = "sqlite3_open_v2")]
    private static extern int Open(byte[] filename, out IntPtr database, int flags, IntPtr vfs);

    [DllImport(LibraryName, EntryPoint = "sqlite3_close_v2")]
    private static extern int Close(IntPtr database);

    [DllImport(LibraryName, EntryPoint = "sqlite3_exec")]
    private static extern int Exec(
        IntPtr database,
        byte[] sql,
        IntPtr callback,
        IntPtr firstArgument,
        out IntPtr errorMessage);

    [DllImport(LibraryName, EntryPoint = "sqlite3_prepare_v2")]
    private static extern int Prepare(
        IntPtr database,
        byte[] sql,
        int byteLength,
        out IntPtr statement,
        out IntPtr tail);

    [DllImport(LibraryName, EntryPoint = "sqlite3_step")]
    private static extern int Step(IntPtr statement);

    [DllImport(LibraryName, EntryPoint = "sqlite3_reset")]
    private static extern int Reset(IntPtr statement);

    [DllImport(LibraryName, EntryPoint = "sqlite3_finalize")]
    private static extern int Finalize(IntPtr statement);

    [DllImport(LibraryName, EntryPoint = "sqlite3_bind_int64")]
    private static extern int BindInt64(IntPtr statement, int index, long value);

    [DllImport(LibraryName, EntryPoint = "sqlite3_bind_double")]
    private static extern int BindDouble(IntPtr statement, int index, double value);

    [DllImport(LibraryName, EntryPoint = "sqlite3_bind_text")]
    private static extern int BindText(
        IntPtr statement,
        int index,
        byte[] value,
        int byteLength,
        IntPtr destructor);

    [DllImport(LibraryName, EntryPoint = "sqlite3_bind_blob")]
    private static extern int BindBlob(
        IntPtr statement,
        int index,
        byte[] value,
        int byteLength,
        IntPtr destructor);

    [DllImport(LibraryName, EntryPoint = "sqlite3_bind_null")]
    private static extern int BindNull(IntPtr statement, int index);

    [DllImport(LibraryName, EntryPoint = "sqlite3_column_type")]
    private static extern int ColumnType(IntPtr statement, int index);

    [DllImport(LibraryName, EntryPoint = "sqlite3_column_int64")]
    private static extern long ColumnInt64(IntPtr statement, int index);

    [DllImport(LibraryName, EntryPoint = "sqlite3_column_double")]
    private static extern double ColumnDouble(IntPtr statement, int index);

    [DllImport(LibraryName, EntryPoint = "sqlite3_column_text")]
    private static extern IntPtr ColumnText(IntPtr statement, int index);

    [DllImport(LibraryName, EntryPoint = "sqlite3_column_blob")]
    private static extern IntPtr ColumnBlob(IntPtr statement, int index);

    [DllImport(LibraryName, EntryPoint = "sqlite3_column_bytes")]
    private static extern int ColumnBytes(IntPtr statement, int index);

    [DllImport(LibraryName, EntryPoint = "sqlite3_column_count")]
    private static extern int ColumnCount(IntPtr statement);

    [DllImport(LibraryName, EntryPoint = "sqlite3_last_insert_rowid")]
    private static extern long LastInsertRowId(IntPtr database);

    [DllImport(LibraryName, EntryPoint = "sqlite3_changes")]
    private static extern int Changes(IntPtr database);

    [DllImport(LibraryName, EntryPoint = "sqlite3_errmsg")]
    private static extern IntPtr ErrorMessage(IntPtr database);

    [DllImport(LibraryName, EntryPoint = "sqlite3_busy_timeout")]
    private static extern int BusyTimeout(IntPtr database, int milliseconds);

    [DllImport(LibraryName, EntryPoint = "sqlite3_free")]
    private static extern void Free(IntPtr pointer);

    internal static IntPtr OpenDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Database path is required.", nameof(path));

        byte[] utf8Path = EncodeUtf8(path);
        int result = Open(utf8Path, out IntPtr database, OpenReadWrite | OpenCreate | OpenFullMutex, IntPtr.Zero);
        if (result != Ok)
        {
            string message = database != IntPtr.Zero ? ReadUtf8CString(ErrorMessage(database)) : "unknown error";
            if (database != IntPtr.Zero) Close(database);
            throw new SqliteException(result, $"Could not open database '{path}': {message}");
        }
        return database;
    }

    internal static void CloseDatabase(IntPtr database)
    {
        if (database == IntPtr.Zero) return;
        int result = Close(database);
        if (result != Ok)
            throw new SqliteException(result, "Could not close the database connection.");
    }

    internal static void ConfigureBusyTimeout(IntPtr database, int milliseconds)
    {
        int result = BusyTimeout(database, milliseconds);
        if (result != Ok)
            throw new SqliteException(result, ReadUtf8CString(ErrorMessage(database)));
    }

    internal static void RunExec(IntPtr database, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL text is required.", nameof(sql));

        int result = Exec(database, EncodeUtf8(sql), IntPtr.Zero, IntPtr.Zero, out IntPtr errorMessage);
        if (result != Ok)
        {
            string message = errorMessage != IntPtr.Zero ? ReadUtf8CString(errorMessage) : "unknown error";
            if (errorMessage != IntPtr.Zero) Free(errorMessage);
            throw new SqliteException(result, message);
        }
    }

    internal static IntPtr PrepareStatement(IntPtr database, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL text is required.", nameof(sql));

        int result = Prepare(
            database,
            EncodeUtf8(sql),
            -1,
            out IntPtr statement,
            out _);
        if (result != Ok || statement == IntPtr.Zero)
        {
            string message = ReadUtf8CString(ErrorMessage(database));
            if (statement != IntPtr.Zero) Finalize(statement);
            throw new SqliteException(result, $"Could not prepare statement '{sql}': {message}");
        }
        return statement;
    }

    internal static int StepStatement(IntPtr database, IntPtr statement)
    {
        int result = Step(statement);
        if (result == Row || result == Done) return result;
        throw new SqliteException(result, ReadUtf8CString(ErrorMessage(database)));
    }

    internal static void ResetStatement(IntPtr statement)
    {
        int result = Reset(statement);
        if (result != Ok)
            throw new SqliteException(result, "Could not reset the statement.");
    }

    internal static int FinalizeStatement(IntPtr statement)
    {
        if (statement == IntPtr.Zero) return Ok;
        return Finalize(statement);
    }

    internal static void BindInteger(IntPtr statement, int index, long value)
    {
        int result = BindInt64(statement, index, value);
        if (result != Ok)
            throw new SqliteException(result, $"Could not bind integer at index {index}.");
    }

    internal static void BindReal(IntPtr statement, int index, double value)
    {
        int result = BindDouble(statement, index, value);
        if (result != Ok)
            throw new SqliteException(result, $"Could not bind real at index {index}.");
    }

    internal static void BindString(IntPtr statement, int index, string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        int result = BindText(statement, index, utf8, utf8.Length, Transient);
        if (result != Ok)
            throw new SqliteException(result, $"Could not bind text at index {index}.");
    }

    internal static void BindBinary(IntPtr statement, int index, byte[] value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        int result = BindBlob(statement, index, value, value.Length, Transient);
        if (result != Ok)
            throw new SqliteException(result, $"Could not bind blob at index {index}.");
    }

    internal static void BindEmpty(IntPtr statement, int index)
    {
        int result = BindNull(statement, index);
        if (result != Ok)
            throw new SqliteException(result, $"Could not bind null at index {index}.");
    }

    internal static bool ColumnIsNull(IntPtr statement, int index) =>
        ColumnType(statement, index) == ColumnTypeNull;

    internal static long ColumnInteger(IntPtr statement, int index) =>
        ColumnInt64(statement, index);

    internal static double ColumnReal(IntPtr statement, int index) =>
        ColumnDouble(statement, index);

    internal static string ColumnString(IntPtr statement, int index)
    {
        IntPtr pointer = ColumnText(statement, index);
        if (pointer == IntPtr.Zero) return null;
        int byteCount = ColumnBytes(statement, index);
        if (byteCount <= 0) return string.Empty;
        byte[] buffer = new byte[byteCount];
        Marshal.Copy(pointer, buffer, 0, byteCount);
        return Encoding.UTF8.GetString(buffer);
    }

    internal static byte[] ColumnBinary(IntPtr statement, int index)
    {
        IntPtr pointer = ColumnBlob(statement, index);
        if (pointer == IntPtr.Zero) return Array.Empty<byte>();
        int byteCount = ColumnBytes(statement, index);
        if (byteCount <= 0) return Array.Empty<byte>();
        byte[] buffer = new byte[byteCount];
        Marshal.Copy(pointer, buffer, 0, byteCount);
        return buffer;
    }

    internal static long ReadLastInsertRowId(IntPtr database) => LastInsertRowId(database);

    internal static int ReadChanges(IntPtr database) => Changes(database);

    internal static string ReadErrorMessage(IntPtr database) => ReadUtf8CString(ErrorMessage(database));

    internal static int ReadColumnCount(IntPtr statement) => ColumnCount(statement);

    private const int ColumnTypeNull = 5;

    private static string ReadUtf8CString(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return string.Empty;
        int length = 0;
        while (Marshal.ReadByte(pointer, length) != 0) length++;
        if (length == 0) return string.Empty;
        byte[] buffer = new byte[length];
        Marshal.Copy(pointer, buffer, 0, length);
        return Encoding.UTF8.GetString(buffer);
    }

    private static byte[] EncodeUtf8(string value)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        byte[] terminated = new byte[utf8.Length + 1];
        Array.Copy(utf8, terminated, utf8.Length);
        return terminated;
    }
}

public sealed class SqliteStatement : IDisposable
{
    private readonly SqliteConnection owner;
    private IntPtr handle;

    internal SqliteStatement(SqliteConnection owner, IntPtr handle)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.handle = handle;
    }

    public SqliteStatement BindInt(int index, int value)
    {
        RequireHandle();
        Sqlite3Native.BindInteger(handle, index, value);
        return this;
    }

    public SqliteStatement BindLong(int index, long value)
    {
        RequireHandle();
        Sqlite3Native.BindInteger(handle, index, value);
        return this;
    }

    public SqliteStatement BindFloat(int index, float value)
    {
        RequireHandle();
        Sqlite3Native.BindReal(handle, index, value);
        return this;
    }

    public SqliteStatement BindDouble(int index, double value)
    {
        RequireHandle();
        Sqlite3Native.BindReal(handle, index, value);
        return this;
    }

    public SqliteStatement BindText(int index, string value)
    {
        RequireHandle();
        Sqlite3Native.BindString(handle, index, value);
        return this;
    }

    public SqliteStatement BindBlob(int index, byte[] value)
    {
        RequireHandle();
        Sqlite3Native.BindBinary(handle, index, value);
        return this;
    }

    public SqliteStatement BindNull(int index)
    {
        RequireHandle();
        Sqlite3Native.BindEmpty(handle, index);
        return this;
    }

    public bool Step()
    {
        RequireHandle();
        int result = Sqlite3Native.StepStatement(owner.Handle, handle);
        if (result == Sqlite3Native.Done) Sqlite3Native.ResetStatement(handle);
        return result == Sqlite3Native.Row;
    }

    public void Reset()
    {
        RequireHandle();
        Sqlite3Native.ResetStatement(handle);
    }

    public int ColumnCount => Sqlite3Native.ReadColumnCount(handle);

    public bool IsNull(int index)
    {
        RequireHandle();
        return Sqlite3Native.ColumnIsNull(handle, index);
    }

    public int ColumnInt(int index)
    {
        RequireHandle();
        return checked((int)Sqlite3Native.ColumnInteger(handle, index));
    }

    public long ColumnLong(int index)
    {
        RequireHandle();
        return Sqlite3Native.ColumnInteger(handle, index);
    }

    public float ColumnFloat(int index)
    {
        RequireHandle();
        return (float)Sqlite3Native.ColumnReal(handle, index);
    }

    public double ColumnDouble(int index)
    {
        RequireHandle();
        return Sqlite3Native.ColumnReal(handle, index);
    }

    public string ColumnText(int index)
    {
        RequireHandle();
        return Sqlite3Native.ColumnString(handle, index);
    }

    public byte[] ColumnBlob(int index)
    {
        RequireHandle();
        return Sqlite3Native.ColumnBinary(handle, index);
    }

    public void Dispose()
    {
        if (handle == IntPtr.Zero) return;
        Sqlite3Native.FinalizeStatement(handle);
        handle = IntPtr.Zero;
    }

    private void RequireHandle()
    {
        if (handle == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(SqliteStatement));
    }
}

public sealed class SqliteConnection : IDisposable
{
    internal IntPtr Handle { get; private set; }

    public SqliteConnection(string path)
    {
        Handle = Sqlite3Native.OpenDatabase(path);
        Sqlite3Native.ConfigureBusyTimeout(Handle, 5000);
    }

    public void Execute(string sql)
    {
        RequireHandle();
        Sqlite3Native.RunExec(Handle, sql);
    }

    public SqliteStatement Prepare(string sql)
    {
        RequireHandle();
        return new SqliteStatement(this, Sqlite3Native.PrepareStatement(Handle, sql));
    }

    public long LastInsertRowId
    {
        get
        {
            RequireHandle();
            return Sqlite3Native.ReadLastInsertRowId(Handle);
        }
    }

    public int Changes
    {
        get
        {
            RequireHandle();
            return Sqlite3Native.ReadChanges(Handle);
        }
    }

    public void BeginTransaction()
    {
        Execute("BEGIN IMMEDIATE;");
    }

    public void Commit()
    {
        Execute("COMMIT;");
    }

    public void Rollback()
    {
        Execute("ROLLBACK;");
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        IntPtr handle = Handle;
        Handle = IntPtr.Zero;
        Sqlite3Native.CloseDatabase(handle);
    }

    private void RequireHandle()
    {
        if (Handle == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(SqliteConnection));
    }
}
