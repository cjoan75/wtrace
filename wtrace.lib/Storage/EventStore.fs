module LowLevelDesign.WTrace.Storage.EventStore

open Microsoft.Data.Sqlite
open LowLevelDesign.WTrace


[<AutoOpen>]
module private Sql =
    type SqliteDataReader with
        member this.GetFieldValue<'T>(name : string) =
            this.GetFieldValue<'T>(this.GetOrdinal(name))

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
        use cmd = conn.CreateCommand(CommandText = sql)
        executeCommand cmd paramDefs

    let executeSqlNoParams (conn : SqliteConnection) sql =
        executeSql conn sql Seq.empty<SqliteParameter>

    let insertBatch (conn : SqliteConnection) sql (cmdParams : seq<SqliteParameter>) getCmdParamValues elems =
        use tran = conn.BeginTransaction()
        use cmd = conn.CreateCommand(CommandText = sql, Transaction = tran)

        cmdParams |> cmd.Parameters.AddRange

        let setCmdParamValues elem = 
             let vals : seq<obj> = getCmdParamValues elem
             vals |> Seq.iteri (fun i v -> cmd.Parameters.[i].Value <- v)
        elems |> Seq.iter (fun elem -> setCmdParamValues elem; cmd.ExecuteNonQuery() |> ignore)

        tran.Commit()


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
    executeSqlNoParams conn sql

    let sql = @"
    create table if not exists TraceEventField (
        EventIndex integer not null,
        Name text not null,
        Type integer not null,
        Value blob not null
    )"
    executeSqlNoParams conn sql


let insertEvents conn events = 
    let sql = @"insert into TraceEvent (EventIndex, TimeStampRelativeMSec, TimeStampQPC, DurationMSec, ProcessId, ProcessName, ThreadId, 
                        ProviderName, TaskName, OpcodeName, EventLevel, Path, Details, Result)
                    values (@EventIndex, @TimeStampRelativeMSec, @TimeStampQPC, @DurationMSec, @ProcessId, @ProcessName, @ThreadId, 
                        @ProviderName, @TaskName, @OpcodeName, @EventLevel, @Path, @Details, @Result)"
    
    let prms = seq {
        SqliteParameter("@EventIndex", SqliteType.Integer);
        SqliteParameter("@TimeStampRelativeMSec", SqliteType.Real);
        SqliteParameter("@TimeStampQPC", SqliteType.Integer);
        SqliteParameter("@DurationMSec", SqliteType.Real);
        SqliteParameter("@ProcessId", SqliteType.Integer);
        SqliteParameter("@ProcessName", SqliteType.Text);
        SqliteParameter("@ThreadId", SqliteType.Integer);
        SqliteParameter("@ProviderName", SqliteType.Text);
        SqliteParameter("@TaskName", SqliteType.Text);
        SqliteParameter("@OpcodeName", SqliteType.Text);
        SqliteParameter("@EventLevel", SqliteType.Integer);
        SqliteParameter("@Path", SqliteType.Text);
        SqliteParameter("@Details", SqliteType.Text);
        SqliteParameter("@Result", SqliteType.Text);
    } 

    let getPrmValues (ev : WTraceEvent) : seq<obj> = 
         seq { ev.EventIndex; ev.TimeStampRelativeMSec; ev.TimeStampQPC; ev.DurationMSec;
               ev.ProcessId; ev.ProcessName; ev.ThreadId; ev.ProviderName; ev.TaskName;
               ev.OpcodeName; ev.EventLevel; ev.Path; ev.Details; ev.Result }
    
    insertBatch conn sql prms getPrmValues events


let insertEventFields conn eventFields =
    let sql = @"insert into TraceEventField (EventIndex, Name, Type, Value) values (@EventIndex, @Name, @Type, @Value)"

    let prms =
        seq {
            SqliteParameter("@EventIndex", SqliteType.Integer);
            SqliteParameter("@Name", SqliteType.Text);
            SqliteParameter("@Type", SqliteType.Integer);
            SqliteParameter("@Value", SqliteType.Blob);
        }

    let getPrmValues (evf : WTraceEventField) : seq<obj> =
         seq { evf.EventIndex; evf.Name; evf.Type; evf.Value }

    insertBatch conn sql prms getPrmValues eventFields


let queryEvents (cmd : SqliteCommand) =
    let decodeEvent (reader : SqliteDataReader) =
        {
            EventIndex = reader.GetFieldValue<uint32>("EventIndex")
            TimeStampRelativeMSec = reader.GetFieldValue<float>("TimeStampRelativeMSec")
            TimeStampQPC = reader.GetFieldValue<int64>("TimeStampQPC")
            DurationMSec = reader.GetFieldValue<float>("DurationMSec")
            ProcessId = reader.GetFieldValue<int32>("ProcessId")
            ProcessName = reader.GetFieldValue<string>("ProcessName")
            ThreadId = reader.GetFieldValue<int32>("ThreadId")
            ProviderName = reader.GetFieldValue<string>("ProviderName")
            TaskName = reader.GetFieldValue<string>("TaskName")
            OpcodeName = reader.GetFieldValue<string>("OpcodeName")
            EventLevel = reader.GetFieldValue<int32>("EventLevel")
            Path = reader.GetFieldValue<string>("Path")
            Result = reader.GetFieldValue<string>("Result")
            Details = reader.GetFieldValue<string>("Details")
        }

    executeQuery cmd decodeEvent


let queryEventFields (cmd : SqliteCommand) =
    let decodeField (reader : SqliteDataReader) = 
        {
            EventIndex = reader.GetFieldValue<uint32>("EventIndex")
            Name = reader.GetFieldValue<string>("Name")
            Type = reader.GetFieldValue<ValueType>("Type")
            Value = reader.GetFieldValue<array<byte>>("Value")
        }
    executeQuery cmd decodeField

