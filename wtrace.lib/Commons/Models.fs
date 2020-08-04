﻿namespace LowLevelDesign.WTrace

open System
open Microsoft.Diagnostics.Tracing.Parsers
open System.Collections.Generic
open Microsoft.Diagnostics.Tracing

type EtwTraceEvent = Microsoft.Diagnostics.Tracing.TraceEvent

type NtKeywords = KernelTraceEventParser.Keywords

type EtwEventHeader = {
    EventIndex : uint32
    TimeStampRelativeMSec : float
    TimeStampQPC : int64
    ProcessId : int32
    ThreadId : int32
    TaskName : string
    OpcodeName : string
    EventLevel : int32
}

(* FUTURE: this part should be understandable for the C# clients too *)

type WTraceEvent = {
    EventIndex : uint32
    TimeStampRelativeMSec : float
    TimeStampQPC : int64
    DurationMSec : float
    ProcessId : int32
    ProcessName : string
    ThreadId : int32
    ProviderName : string
    TaskName : string
    OpcodeName : string
    EventLevel : int32
    Path : string
    Details : string
    Result : string
    Payload : array<byte>
}

type IDisposableObservable<'T> =
    inherit IObservable<'T>
    inherit IDisposable

type EtwProviderRegistration = {
    Id : Guid
    Keywords : uint64
    RegisterParser : TraceEventSource -> IObservable<EtwTraceEvent>
}

type ITraceEtwHandler = 
    abstract member KernelFlags : NtKeywords

    abstract member KernelStackFlags : NtKeywords

    abstract member UserModeProviders : IEnumerable<EtwProviderRegistration>

    abstract member Observe : IObservable<EtwTraceEvent> -> IDisposableObservable<WTraceEvent>

(* end of the C# understandable part *)

type WTraceCodeAddress = {
    CodeAddressIndex : int32
    OffsetInMethod : int32 // RVA or IL offset from the method base address
    HasSourceFileInfo : bool
    FullName : string
    // TODO: additional fields that could allow offline
    // symbol resolution in the future
}

type WTraceEventCallStack = {
    EventIndex : uint32
    CallStack : array<WTraceCodeAddress>
}

