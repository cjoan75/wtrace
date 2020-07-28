namespace LowLevelDesign.WTrace.Events

open System
open System.Collections.Generic
open System.IO
open System.Diagnostics
open System.Reactive
open System.Reactive.Subjects
open FSharp.Control.Reactive
open Microsoft.Diagnostics.Tracing.Parsers.Kernel
open LowLevelDesign.WTrace
open LowLevelDesign.WTrace.WinApi

#nowarn "44" // disable the deprecation warning as we want to use TimeStampQPC

type FileIoEvent = 
| FileIoPending of IrpPtr : uint64 * Event : EtwTraceEvent
| FileIoCompleted of IrpPtr : uint64 * Event : FileIOOpEndTraceData
| Skipped

type FileIoObservable (sessionObservable : IObservable<EtwTraceEvent>) as this =
    let encodingUTF8 = System.Text.Encoding.UTF8

    let fileIOTaskGuid = Guid(int32(0x90cbdc39), int16(0x4a3e), int16(0x11d1), byte(0x84), byte(0xf4), byte(0x00), byte(0x00), byte(0xf8), byte(0x04), byte(0x64), byte(0xe3))
 
    let logger = TraceSource("WTrace.ETW.FileIO")

    let subscription = sessionObservable |> Observable.subscribeObserver this
    let subject = new Subjects.Subject<WTraceEvent>()

    // a state to keep information about the pending IO requests
    // FIXME: make sure we remove old events from time to time
    let state = Dictionary<uint64, EtwTraceEvent>()

    // CHECKME: what about DiskIOTraceData and DiskIOInitTraceData - do we need those?
    let toFileIoEvent (ev : EtwTraceEvent) = 
        if ev.TaskGuid = fileIOTaskGuid then
            match ev with
            | :? FileIOCreateTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
            | :? FileIODirEnumTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
            | :? FileIOInfoTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
            | :? FileIOOpEndTraceData as ev -> FileIoCompleted (ev.IrpPtr, ev)
            | :? FileIOReadWriteTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
            | :? FileIOSimpleOpTraceData as ev -> FileIoPending (ev.IrpPtr, ev)
            | _ -> Skipped
        else Skipped

    let createWTraceEvent (ev : EtwTraceEvent) (completion : FileIOOpEndTraceData) = 
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

        let evind = uint32 ev.EventIndex
        let path, details, fields = 
            match ev with
            | :? FileIOCreateTraceData as ev ->
                let attrStr = fileAttrStr ev.FileAttributes
                let fields = 
                    [|
                        { EventIndex = evind; Name = nameof ev.IrpPtr; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.IrpPtr) }
                        { EventIndex = evind; Name = nameof ev.FileObject; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.FileObject) }
                        // CHECKME (nameof ev.CreateOptions) - not sure how to print it
                        { EventIndex = evind; Name = nameof ev.CreateDispostion; Type = ValueType.String; Value = encodingUTF8.GetBytes(ev.CreateDispostion.ToString()) }
                        { EventIndex = evind; Name = nameof ev.ShareAccess; Type = ValueType.String; Value = encodingUTF8.GetBytes(fileShareStr ev.ShareAccess) }
                        { EventIndex = evind; Name = nameof ev.FileAttributes; Type = ValueType.String; Value = encodingUTF8.GetBytes(attrStr) }
                    |]
                let details = sprintf "IRP: 0x%X, attributes: %s" ev.IrpPtr attrStr
                (ev.FileName, details, fields)
            | :? FileIODirEnumTraceData as ev ->
                let details = sprintf "Directory: '%s', FileIndex: %d, IRP: 0x%X" ev.DirectoryName ev.FileIndex ev.IrpPtr
                let fields =
                    [|
                        { EventIndex = evind; Name = nameof ev.IrpPtr; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.IrpPtr) }
                        { EventIndex = evind; Name = nameof ev.FileObject; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.FileObject) }
                        { EventIndex = evind; Name = nameof ev.DirectoryName; Type = ValueType.String; Value = encodingUTF8.GetBytes(ev.DirectoryName) }
                        { EventIndex = evind; Name = nameof ev.FileKey; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.FileKey) }
                        { EventIndex = evind; Name = nameof ev.FileIndex; Type = ValueType.Integer; Value = BitConverter.GetBytes(ev.FileIndex) }
                    |]
                (ev.FileName, details, fields)
            | :? FileIOInfoTraceData as ev ->
                let details = sprintf "IRP: 0x%X" ev.IrpPtr
                let fields =
                    [|
                        { EventIndex = evind; Name = nameof ev.IrpPtr; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.IrpPtr) }
                        { EventIndex = evind; Name = nameof ev.FileObject; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.FileObject) }
                    |]
                (ev.FileName, details, fields)
            | :? FileIOReadWriteTraceData as ev ->
                let details = sprintf "IRP: 0x%X, offset: %d, I/O size: %d" ev.IrpPtr ev.Offset ev.IoSize
                let fields =
                    [|
                        { EventIndex = evind; Name = nameof ev.IrpPtr; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.IrpPtr) }
                        { EventIndex = evind; Name = nameof ev.FileObject; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.FileObject) }
                        { EventIndex = evind; Name = nameof ev.IoSize; Type = ValueType.Integer; Value = BitConverter.GetBytes(ev.IoSize) }
                        { EventIndex = evind; Name = nameof ev.Offset; Type = ValueType.Integer; Value = BitConverter.GetBytes(ev.Offset) }
                        { EventIndex = evind; Name = nameof ev.IoFlags; Type = ValueType.HexNumber; Value = BitConverter.GetBytes(ev.IoFlags) }
                        { EventIndex = evind; Name = "Bytes"; Type = ValueType.Integer; Value = BitConverter.GetBytes(completion.ExtraInfo) }
                    |]
                (ev.FileName, details, fields)
            | :? FileIOSimpleOpTraceData as ev ->
                let details = sprintf "IRP: 0x%X" ev.IrpPtr
                let fields =
                    [|
                        { EventIndex = evind; Name = nameof ev.IrpPtr; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.IrpPtr) }
                        { EventIndex = evind; Name = nameof ev.FileObject; Type = ValueType.Address; Value = BitConverter.GetBytes(ev.FileObject) }
                    |]
                (ev.FileName, details, fields)
            | _ -> assert false; ("Invalid data", String.Empty, Array.empty<WTraceEventField>)
  
        {
            EventIndex = evind
            TimeStampRelativeMSec = ev.TimeStampRelativeMSec
            TimeStampQPC = ev.TimeStampQPC
            DurationMSec = completion.TimeStampRelativeMSec - ev.TimeStampRelativeMSec
            ProcessId = ev.ProcessID
            ProcessName = ev.ProcessName
            ThreadId = ev.ThreadID
            ProviderName = ev.ProviderName
            TaskName = ev.TaskName
            OpcodeName = ev.OpcodeName
            EventLevel = int32 ev.Level
            Path = path
            Details = details
            Result = Win32Error.GetName(typedefof<Win32Error>, completion.NtStatus) |? (sprintf "0x%X" completion.NtStatus)
            Fields = fields
        }

    interface IObserver<EtwTraceEvent> with
        member _.OnNext(ev) =
            match toFileIoEvent ev with
            | FileIoPending (irp, ev) ->
                assert (not (state.ContainsKey(irp)))
                state.[irp] <- ev
            | FileIoCompleted (irp, ev) ->
                match state.TryGetValue(irp) with
                | true, prevEvent -> 
                    state.Remove(irp) |> ignore
                    subject.OnNext(createWTraceEvent prevEvent ev)
                | false, _ -> 
                        logger.TraceWarning(sprintf "missing past event for IRP: 0x%X" irp)
            | _ -> () // skip all unknown events

        member _.OnError(ex) = subject.OnError(ex)

        member _.OnCompleted() = assert false // the ETW observables do not send the OnCompleted events

    interface IDisposableObservable<WTraceEvent> with
        member _.Subscribe(o) =
            subject |> Observable.subscribeObserver o

        member _.Dispose() =
            subscription.Dispose()
            subject.Dispose()


type FileIoEtwHandler () =

    interface ITraceEtwHandler with

        member _.KernelFlags with get() = NtKeywords.FileIOInit ||| NtKeywords.FileIO ||| 
                                          NtKeywords.DiskFileIO ||| NtKeywords.DiskIO ||| NtKeywords.DiskIOInit

        member _.KernelStackFlags with get() = NtKeywords.FileIO

        member _.UserModeProviders with get() = Seq.empty<EtwProviderRegistration>

        member _.Observe observable =
            new FileIoObservable(observable) :> IDisposableObservable<WTraceEvent>

