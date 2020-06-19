﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Tx.Windows.Etw
{
    class EtwNativeConstants
    {
        public const Int32 ErrorNotFound = 0x2; //0x000000a1 ?;
        public const Int32 ErrorUnreadable = 0x00000570;
        public const uint TraceModeRealTime = 0x00000100;
        public const uint TraceModeEventRecord = 0x10000000;

        public const UInt16 EVENT_HEADER_FLAG_32_BIT_HEADER = 0x20;
        public const UInt16 EVENT_HEADER_FLAG_64_BIT_HEADER = 0x40;
        public const UInt16 EVENT_HEADER_FLAG_PROCESSOR_INDEX = 0x0200;

        public static readonly ulong InvalidHandle = (Environment.OSVersion.Version.Major >= 6
                                                          ? 0x00000000FFFFFFFF
                                                          : 0xFFFFFFFFFFFFFFFF);

        internal const uint EVENT_TRACE_REAL_TIME_MODE = 0x00000100;
        // private sessions or private logger information.   Sadly, these are not very useful because they don't work for real time.  
        // TODO USE or remove.   See http://msdn.microsoft.com/en-us/library/windows/desktop/aa363689(v=vs.85).aspx
        // Unfortunately they only work for file based logging (not real time) so they are of limited value.  
        // internal const uint EVENT_TRACE_PRIVATE_LOGGER_MODE = 0x00000800;
        // internal const uint EVENT_TRACE_PRIVATE_IN_PROC = 0x00020000;

        //  EVENT_TRACE_LOGFILE.LogFileMode should be set to PROCESS_TRACE_MODE_EVENT_RECORD 
        //  to consume events using EventRecordCallback
        internal const uint PROCESS_TRACE_MODE_EVENT_RECORD = 0x10000000;
        internal const uint PROCESS_TRACE_MODE_REAL_TIME = 0x00000100;
        internal const uint PROCESS_TRACE_MODE_RAW_TIMESTAMP = 0x00001000;

        internal const uint EVENT_TRACE_FILE_MODE_NONE = 0x00000000;
        internal const uint EVENT_TRACE_FILE_MODE_SEQUENTIAL = 0x00000001;
        internal const uint EVENT_TRACE_FILE_MODE_CIRCULAR = 0x00000002;
        internal const uint EVENT_TRACE_FILE_MODE_APPEND = 0x00000004;
        internal const uint EVENT_TRACE_FILE_MODE_NEWFILE = 0x00000008;
        internal const uint EVENT_TRACE_BUFFERING_MODE = 0x00000400;
        internal const uint EVENT_TRACE_INDEPENDENT_SESSION_MODE = 0x08000000;

        internal const uint EVENT_TRACE_CONTROL_QUERY = 0;
        internal const uint EVENT_TRACE_CONTROL_STOP = 1;
        internal const uint EVENT_TRACE_CONTROL_UPDATE = 2;
        internal const uint EVENT_TRACE_CONTROL_FLUSH = 3;

        internal const uint WNODE_FLAG_TRACED_GUID = 0x00020000;
        internal const uint EVENT_TRACE_SYSTEM_LOGGER_MODE = 0x02000000;


    }
}
