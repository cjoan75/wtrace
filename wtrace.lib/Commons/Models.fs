namespace LowLevelDesign.WTrace

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

type ValueType = 
| Blob = 0
| Integer = 1 // 64-bit
| Float = 2 // 64-bit
| String = 3
| HexNumber = 4
| Address = 5

type WTraceEventField = {
    EventIndex : uint32
    Name : string
    Type : ValueType
    Value : array<byte>
}

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
}

type WTraceEventWithFields = {
    Event : WTraceEvent
    Fields : array<WTraceEventField>
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

    abstract member Observe : IObservable<EtwTraceEvent> -> IDisposableObservable<WTraceEventWithFields>

(* end of the C# understandable part *)

type TraceCallstack = {
    EventIndex : uint32 // points to the event which "owns" the stack
    ProcessId : int32
    ThreadId : int32
}

