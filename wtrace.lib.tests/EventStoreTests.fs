
namespace LowLevelDesign.WTrace.Tests.Storage

open System
open System.IO
open NUnit.Framework
open FsUnit
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.Storage


[<SetUpFixture>]
type EventStoreTestsSetupUp () =

    let path = Path.Combine(Path.GetTempPath(), "wtrace.db")
    
    [<OneTimeSetUp>]
    member _.Setup () =
        use conn = EventsDatabase.openConnection path
        conn |> EventsDatabase.createOrUpdateDataModel

    [<OneTimeTearDown>]
    member _.TearDown () =
        if File.Exists(path) then
            File.Delete(path)


module EventStoreTests =
    let path = Path.Combine(Path.GetTempPath(), "wtrace.db")

    [<Test>]
    let TestSavingEvent () =
        use conn = EventsDatabase.openConnection path

        let event = {
            EventIndex = 1u
            TimeStampRelativeMSec = 1.0
            TimeStampQPC = 1L
            DurationMSec = 1.0
            ProcessId = 1234
            ProcessName = "notepad"
            ThreadId = 1
            ProviderName = "TestProvider"
            TaskName = "Event"
            OpcodeName = "Emit"
            EventLevel = 0
            Path = "non-existing-path"
            Details = "short details"
            Result = "SUCCESS"
            Payload = Array.empty<byte>
        }

        Seq.singleton event |> EventsDatabase.insertEvents conn

        use cmd = conn.CreateCommand(CommandText = "select * from TraceEvent")
        let events = cmd |> EventsDatabase.queryEventsNoFields |> Seq.toArray

        events.Length |> should equal 1
        events.[0] |> should equal event


