module LowLevelDesign.WTrace.Storage.EventsDb

open Microsoft.Data.Sqlite

module private Sql =
    let executeCommand (cmd : SqliteCommand) (sqlParams : seq<SqliteParameter>) =
        sqlParams |> cmd.Parameters.AddRange
        cmd.ExecuteNonQuery() |> ignore
        
    let executeQuery (cmd : SqliteCommand) decodeRow = 
        seq { 
            use reader = cmd.ExecuteReader()
            while reader.Read() do 
                yield decodeRow reader
        }

    let executeSql (conn : SqliteConnection) sql paramDefs = 
        let cmd = conn.CreateCommand(CommandText = sql)
        executeCommand cmd paramDefs

    let executeSqlNoParams (conn : SqliteConnection) sql =
        executeSql conn sql Seq.empty<SqliteParameter>


let openConnection (path : string) =
    
    let conn = new SqliteConnection(sprintf @"Data Source=%s" path)
    // FIXME change the journal settings
    conn.Open()
    conn


let createOrUpdateDataModel (conn : SqliteConnection) = 
    let sql = @"
    create table if not exists TraceEvent (
        EventIndex integer not null,
        TimeStampRelativeMSec real not null,
        TimeStampQPC integer not null,
        DurationMSec real not null,
        ProcessId integer not null,
        ProcessName text not null,
        ThreadId integer not null,
        ProviderName text not null,
        TaskName text not null,
        OpcodeName text not null,
        EventLevel integer not null,
        Path text not null,
        Details text not null,
        Result text not null
    )"
    Sql.executeSqlNoParams conn sql

    let sql = @"
    create table if not exists TraceEventField (
        EventIndex integer not null,
        Type integer not null,
        Value blob not null
    )"
    Sql.executeSqlNoParams conn sql


(*
let insertEvents (conn : SqliteConnection) etwEvents = 
    let sql = @"insert into EtwEvent (TimeStamp, ProviderId, ProcessId, ThreadId, Channel, Level, Opcode, Task, Keyword, UserData)
                    values (@TimeStamp, @ProviderId, @ProcessId, @ThreadId, @Channel, @Level, @Opcode, @Task, @Keyword, @UserData)"
    use tran = conn.BeginTransaction()
    use cmd = conn.CreateCommand(CommandText = sql, Transaction = tran)
    
    seq {
        SqliteParameter("@TimeStamp", SqliteType.Text);
        SqliteParameter("@ProviderId", SqliteType.Text);
        SqliteParameter("@ProcessId", SqliteType.Integer);
        SqliteParameter("@ThreadId", SqliteType.Integer);
        SqliteParameter("@Channel", SqliteType.Integer);
        SqliteParameter("@Level", SqliteType.Integer);
        SqliteParameter("@Opcode", SqliteType.Integer);
        SqliteParameter("@Task", SqliteType.Integer);
        SqliteParameter("@Keyword", SqliteType.Integer);
        SqliteParameter("@UserData", SqliteType.Blob)
    } |> cmd.Parameters.AddRange
    
    etwEvents |> Seq.iter (fun ev ->
        let vals : array<obj> = [| ev.TimeStamp; ev.ProviderId; ev.ProcessId; 
                                   ev.ThreadId; ev.Channel; ev.Level; ev.Opcode;
                                   ev.Task; ev.Keyword; ev.UserData |]
        vals |> Array.iteri (fun i v -> cmd.Parameters.[i].Value <- v)
        cmd.ExecuteNonQuery() |> ignore)
    
    tran.Commit()
*)
