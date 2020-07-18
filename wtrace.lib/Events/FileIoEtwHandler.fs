namespace LowLevelDesign.WTrace.Events

open System
open System.Collections.Generic
open System.IO
open System.Diagnostics
open System.Text
open System.Reactive.Subjects
open FSharp.Control.Reactive
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open System.Reactive

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

type FileIoEvent = 
| FileIoPending of IrpPtr : uint64 * Event : EtwTraceEvent
| FileIoCompleted of IrpPtr : uint64 * Event : FileIOOpEndTraceData
| Skipped

type FileIoObservable (sessionObservable : IObservable<EtwTraceEvent>) as this =
    
    let logger = TraceSource("WTrace.ETW.FileIO")
    
    let subscription = sessionObservable |> Observable.subscribeObserver this
    let subject = new Subjects.Subject<TraceEvent>()

    // a state to keep information about the pending IO requests
    // FIXME: make sure we remove old events from time to time
    let state = Dictionary<uint64, EtwTraceEvent>()

    let getFieldInfo (ev : EtwTraceEvent) (fieldName, fieldType) =
        let v = ev.PayloadByName(fieldName)
        if v = null then None
        else
            try
                match fieldType with
                | ValueType.Integer -> 
                    let b = BitConverter.GetBytes(Convert.ToInt64(v))
                    Some { Name = fieldName; Type = fieldType; Value = b }
                | ValueType.String ->
                    let b = Encoding.UTF8.GetBytes(v.ToString())
                    Some { Name = fieldName; Type = fieldType; Value = b }
                | _ -> None
            with
            | :? InvalidCastException -> 
                logger.TraceWarning(sprintf "invalid cast for field: '%s', event: '%s'" fieldName ev.EventName)
                None
            | :? OverflowException ->
                logger.TraceWarning(sprintf "overflow for field: '%s', event: '%s'" fieldName ev.EventName)
                None

    // FIXME: what about DiskIOTraceData and DiskIOInitTraceData - do we need those?
    let toFileIoEvent (ev : EtwTraceEvent) = 
        match ev with
        | :? FileIOCreateTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
        | :? FileIODirEnumTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
        | :? FileIOInfoTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
        | :? FileIOOpEndTraceData as ev -> FileIoCompleted (ev.IrpPtr, ev)
        | :? FileIOReadWriteTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
        | :? FileIOSimpleOpTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
        | _ -> Skipped

    let getEventDetails (ev : EtwTraceEvent) = 
        let fileShareStr (fs : FileShare) =
            if (fs &&& FileShare.ReadWrite &&& FileShare.Delete <> FileShare.None) then "rwd"
            elif (fs &&& FileShare.ReadWrite <> FileShare.None) then "rw-"
            elif (fs &&& FileShare.Read <> FileShare.None) then "r--"
            elif (fs &&& FileShare.Write <> FileShare.None) then "-w-"
            else "---"

        let fileAttrStr (attr : FileAttributes) =
            seq {
                (FileAttributes.Archive, ":A")
                (FileAttributes.Compressed, ":C")
                (FileAttributes.Device, ":DEV")
                (FileAttributes.Directory, ":D")
                (FileAttributes.Encrypted, ":ENC")
                (FileAttributes.Hidden, ":H")
                (FileAttributes.IntegrityStream, ":ISR")
                (FileAttributes.NoScrubData, ":NSD")
                (FileAttributes.NotContentIndexed, ":NCI")
                (FileAttributes.Offline, ":OFF")
                (FileAttributes.ReadOnly, ":RO")
                (FileAttributes.ReparsePoint, ":RP")
                (FileAttributes.SparseFile, ":SP")
                (FileAttributes.System, ":SYS")
                (FileAttributes.Temporary, ":TMP")
            } |> Seq.fold (fun b (a, str) -> if int32(attr &&& a) <> 0 then b + str else b) ""

        match ev with
        | :? FileIOCreateTraceData as ev ->
            let attrStr = fileAttrStr ev.FileAttributes
            let fields = 
                seq {
                    (nameof ev.IrpPtr, ValueType.Address) |> getFieldInfo ev
                    (nameof ev.FileObject, ValueType.Address) |> getFieldInfo ev
                    // FIXME (nameof ev.CreateOptions) - not sure how to print it
                    (nameof ev.CreateDispostion, ValueType.String) |> getFieldInfo ev
                    Some { Name = nameof ev.ShareAccess; Type = ValueType.String; Value = Encoding.UTF8.GetBytes(fileShareStr ev.ShareAccess) }
                    Some { Name = nameof ev.FileAttributes; Type = ValueType.String; Value = Encoding.UTF8.GetBytes(attrStr) }
                } |> Seq.filter Option.isSome |> Seq.map Option.get |> Seq.toArray
            let details = sprintf "Irp: 0x%X, FileObject: 0x%X, attributes: %s" ev.IrpPtr ev.FileObject attrStr
            (ev.FileName, details, fields)
        | _ -> ("", "", Array.empty<TraceEventField>)

    interface IObserver<EtwTraceEvent> with
        member _.OnNext(ev) =
            match toFileIoEvent ev with
            | FileIoPending (irp, ev) ->
                state.Add(irp, ev)
            | FileIoCompleted (irp, ev) ->
                match state.TryGetValue(irp) with
                | true, prevEvent -> 
                    state.Remove(irp) |> ignore
                    let path, details, fields = getEventDetails prevEvent
                    let ev = {
                        EventIndex = uint32 prevEvent.EventIndex
                        TimeStampRelativeMSec = prevEvent.TimeStampRelativeMSec
                        TimeStampQPC = prevEvent.TimeStampQPC
                        DurationMSec = ev.TimeStampRelativeMSec - prevEvent.TimeStampRelativeMSec
                        ProcessId = prevEvent.ProcessID
                        ProcessName = prevEvent.ProcessName
                        ThreadId = prevEvent.ThreadID
                        TaskName = prevEvent.TaskName
                        OpcodeName = prevEvent.OpcodeName
                        EventLevel = int32 prevEvent.Level
                        Path = path
                        Details = details
                        Result = ev.NtStatus.ToString() // FIXME - provider nice name for status
                        Fields = fields
                    }
                    subject.OnNext(ev)
                | false, _ -> 
                        logger.TraceWarning(sprintf "missing past event for IRP: 0x%X" irp)
            | _ -> () // skip all unknown events

        member _.OnError(ex) = subject.OnError(ex)

        member _.OnCompleted() = subject.OnCompleted()

    interface IDisposableObservable<TraceEvent> with
        member _.Subscribe(o) =
            subject |> Observable.subscribeObserver o

        member _.Dispose() =
            subscription.Dispose()
            subject.Dispose()


type FileIoEtwHandler () =

    interface ITraceEtwHandler with

        member _.KernelFlags with get() = NtKeywords.FileIOInit ||| NtKeywords.DiskFileIO ||| NtKeywords.DiskIO ||| NtKeywords.DiskIOInit

        member _.KernelStackFlags with get() = NtKeywords.FileIO

        member _.Observe(observable) =
            new FileIoObservable(observable) :> IDisposableObservable<TraceEvent>

