// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.
// With modifications by Sebastian Solnica (@lowleveldesign)

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Tx.Windows
{
    [SuppressUnmanagedCodeSecurity]
    internal unsafe static class EtwNativeMethods
    {
        [DllImport("advapi32.dll", ExactSpelling = true, EntryPoint = "OpenTraceW", SetLastError = true,
            CharSet = CharSet.Unicode)]
        public static extern UInt64 OpenTrace(ref EVENT_TRACE_LOGFILE logfile);

        [DllImport("advapi32.dll", ExactSpelling = true, EntryPoint = "ProcessTrace")]
        public static extern Int32 ProcessTrace(UInt64[] HandleArray,
                                                UInt32 HandleCount,
                                                IntPtr StartTime,
                                                IntPtr EndTime);

        [DllImport("advapi32.dll", ExactSpelling = true, EntryPoint = "CloseTrace")]
        public static extern Int32 CloseTrace(UInt64 traceHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int StartTraceW(
            [Out] out UInt64 sessionHandle,
            [In] string sessionName,
            EVENT_TRACE_PROPERTIES* properties);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int ControlTrace(
            ulong sessionHandle,
            string sessionName,
            EVENT_TRACE_PROPERTIES* properties,
            uint controlCode);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int EnableTrace(
            [In] uint enable,
            [In] int enableFlag,
            [In] int enableLevel,
            [In] ref Guid controlGuid,
            [In] ulong sessionHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int EnableTraceEx(
            [In] ref Guid ProviderId,
            [In] Guid* SourceId,
            [In] ulong TraceHandle,
            [In] int IsEnabled,
            [In] byte Level,
            [In] ulong MatchAnyKeyword,
            [In] ulong MatchAllKeyword,
            [In] uint EnableProperty,
            [In] EVENT_FILTER_DESCRIPTOR* filterData);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        internal static extern int EnableTraceEx2(
            [In] ulong TraceHandle,
            [In] ref Guid ProviderId,
            [In] uint ControlCode,          // See EVENT_CONTROL_CODE_*
            [In] byte Level,
            [In] ulong MatchAnyKeyword,
            [In] ulong MatchAllKeyword,
            [In] int Timeout,
            [In] ref ENABLE_TRACE_PARAMETERS EnableParameters);

        [DllImport("tdh.dll", ExactSpelling = true, EntryPoint = "TdhGetEventInformation")]
        public static extern Int32 TdhGetEventInformation(
            ref EVENT_RECORD Event,
            UInt32 TdhContextCount,
            IntPtr TdhContext,
            [Out] IntPtr eventInfoPtr,
            ref Int32 BufferSize);

        [DllImport("tdh.dll", ExactSpelling = true, EntryPoint = "TdhGetEventMapInformation")]
        public static extern Int32 TdhGetEventMapInformation(
           ref EVENT_RECORD pEvent,
           IntPtr pMapName,
           [Out] IntPtr eventMapInfoPtr,
           ref Int32 BufferSize);
    }
}