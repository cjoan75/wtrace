﻿// Sources imported from the Tx.Windows and Perfview projects with my modifications

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Tx.Windows
{
    [SuppressUnmanagedCodeSecurity]
    internal delegate void PEVENT_RECORD_CALLBACK([In] ref EVENT_RECORD eventRecord);

    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct Win32TimeZoneInfo
    {
        public Int32 Bias;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public char[] StandardName;
        public SystemTime StandardDate;
        public Int32 StandardBias;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public char[] DaylightName;
        public SystemTime DaylightDate;
        public Int32 DaylightBias;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemTime
    {
        public Int16 Year;
        public Int16 Month;
        public Int16 DayOfWeek;
        public Int16 Day;
        public Int16 Hour;
        public Int16 Minute;
        public Int16 Second;
        public Int16 Milliseconds;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct TRACE_LOGFILE_HEADER
    {
        public UInt32 BufferSize;
        public UInt32 Version;
        public UInt32 ProviderVersion;
        public UInt32 NumberOfProcessors;
        public Int64 EndTime;
        public UInt32 TimerResolution;
        public UInt32 MaximumFileSize;
        public UInt32 LogFileMode;
        public UInt32 BuffersWritten;
        public UInt32 StartBuffers;
        public UInt32 PointerSize;
        public UInt32 EventsLost;
        public UInt32 CpuSpeedInMHz;
        public IntPtr LoggerName;
        public IntPtr LogFileName;
        public Win32TimeZoneInfo TimeZone;
        public Int64 BootTime;
        public Int64 PerfFreq;
        public Int64 StartTime;
        public UInt32 ReservedFlags;
        public UInt32 BuffersLost;
    }

    [Serializable]
    internal enum PROPERTY_FLAGS
    {
        PropertyStruct = 0x1,
        PropertyParamLength = 0x2,
        PropertyParamCount = 0x4,
        PropertyWBEMXmlFragment = 0x8,
        PropertyParamFixedLength = 0x10
    }

    [Serializable]
    internal enum TdhInType : ushort
    {
        Null,
        UnicodeString,
        AnsiString,
        Int8,
        UInt8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float,
        Double,
        Boolean,
        Binary,
        Guid,
        Pointer,
        FileTime,
        SystemTime,
        SID,
        HexInt32,
        HexInt64, // End of winmeta intypes
        CountedString = 300, // Start of TDH intypes for WBEM
        CountedAnsiString,
        ReversedCountedString,
        ReversedCountedAnsiString,
        NonNullTerminatedString,
        NonNullTerminatedAnsiString,
        UnicodeChar,
        AnsiChar,
        SizeT,
        HexDump,
        WbemSID
    };

    [Serializable]
    internal enum TdhOutType : ushort
    {
        Null,
        String,
        DateTime,
        Byte,
        UnsignedByte,
        Short,
        UnsignedShort,
        Int,
        UnsignedInt,
        Long,
        UnsignedLong,
        Float,
        Double,
        Boolean,
        Guid,
        HexBinary,
        HexInt8,
        HexInt16,
        HexInt32,
        HexInt64,
        PID,
        TID,
        PORT,
        IPV4,
        IPV6,
        SocketAddress,
        CimDateTime,
        EtwTime,
        Xml,
        ErrorCode, // End of winmeta outtypes
        ReducedString = 300, // Start of TDH outtypes for WBEM
        NoPrint
    };

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    internal struct EVENT_PROPERTY_INFO
    {
        [FieldOffset(0)] public PROPERTY_FLAGS Flags;
        [FieldOffset(4)] public UInt32 NameOffset;

        [StructLayout(LayoutKind.Sequential)]
        public struct NonStructType
        {
            public TdhInType InType;
            public TdhOutType OutType;
            public UInt32 MapNameOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct StructType
        {
            public UInt16 StructStartIndex;
            public UInt16 NumOfStructMembers;
            private readonly UInt32 _Padding;
        }

        [FieldOffset(8)] public NonStructType NonStructTypeValue;
        [FieldOffset(8)] public StructType StructTypeValue;

        [FieldOffset(16)] public UInt16 CountPropertyIndex;
        [FieldOffset(18)] public UInt16 LengthPropertyIndex;
        [FieldOffset(20)] private UInt32 _Reserved;
    }

    [Serializable]
    internal enum TEMPLATE_FLAGS
    {
        TemplateEventDdata = 1,
        TemplateUserData = 2
    }

    [Serializable]
    internal enum DECODING_SOURCE
    {
        DecodingSourceXmlFile,
        DecodingSourceWbem,
        DecodingSourceWPP
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct TRACE_EVENT_INFO
    {
        public Guid ProviderGuid;
        public Guid EventGuid;
        public EVENT_DESCRIPTOR EventDescriptor;
        public DECODING_SOURCE DecodingSource;
        public UInt32 ProviderNameOffset;
        public UInt32 LevelNameOffset;
        public UInt32 ChannelNameOffset;
        public UInt32 KeywordsNameOffset;
        public UInt32 TaskNameOffset;
        public UInt32 OpcodeNameOffset;
        public UInt32 EventMessageOffset;
        public UInt32 ProviderMessageOffset;
        public UInt32 BinaryXmlOffset;
        public UInt32 BinaryXmlSize;
        public UInt32 ActivityIDNameOffset;
        public UInt32 RelatedActivityIDNameOffset;
        public UInt32 PropertyCount;
        public UInt32 TopLevelPropertyCount;
        public TEMPLATE_FLAGS Flags;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct EVENT_TRACE_HEADER
    {
        public UInt16 Size;
        public UInt16 FieldTypeFlags;
        public UInt32 Version;
        public UInt32 ThreadId;
        public UInt32 ProcessId;
        public Int64 TimeStamp;
        public Guid Guid;
        public UInt32 KernelTime;
        public UInt32 UserTime;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct EVENT_TRACE
    {
        public EVENT_TRACE_HEADER Header;
        public UInt32 InstanceId;
        public UInt32 ParentInstanceId;
        public Guid ParentGuid;
        public IntPtr MofData;
        public UInt32 MofLength;
        public UInt32 ClientContext;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct EVENT_TRACE_LOGFILE
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string LogFileName;
        [MarshalAs(UnmanagedType.LPWStr)] public string LoggerName;
        public Int64 CurrentTime;
        public UInt32 BuffersRead;
        public UInt32 ProcessTraceMode;
        public EVENT_TRACE CurrentEvent;
        public TRACE_LOGFILE_HEADER LogfileHeader;
        public IntPtr BufferCallback;
        public UInt32 BufferSize;
        public UInt32 Filled;
        public UInt32 EventsLost;
        [MarshalAs(UnmanagedType.FunctionPtr)] public PEVENT_RECORD_CALLBACK EventRecordCallback;
        public UInt32 IsKernelTrace;
        public IntPtr Context;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct EVENT_DESCRIPTOR
    {
        public UInt16 Id;
        public byte Version;
        public byte Channel;
        public byte Level;
        public byte Opcode;
        public UInt16 Task;
        public UInt64 Keyword;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct EVENT_HEADER
    {
        public UInt16 Size;
        public UInt16 HeaderType;
        public UInt16 Flags;
        public UInt16 EventProperty;
        public UInt32 ThreadId;
        public UInt32 ProcessId;
        public Int64 TimeStamp;
        public Guid ProviderId;
        public EVENT_DESCRIPTOR EventDescriptor;
        public UInt64 ProcessorTime;
        public Guid ActivityId;
    }

    [Serializable]
    public enum EventHeaderExtType : ushort
    {
        RelatedActivityId = 1,
        Sid,
        TsSid,
        InstanceInfo,
        StackTrace32,
        StackTrace64,
        PebsIndex,
        PmcCounters,
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct EventHeaderExtendedDataItem
    {
        private readonly UInt16 Reserved1;
        public EventHeaderExtType ExtType;
        private readonly UInt16 Reserved2;
        public UInt16 DataSize;
        public IntPtr DataPtr;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct EVENT_RECORD
    {
        public EVENT_HEADER EventHeader;
        public ETW_BUFFER_CONTEXT BufferContext;
        public UInt16 ExtendedDataCount;
        public UInt16 UserDataLength;
        public IntPtr ExtendedData;
        public IntPtr UserData;
        public IntPtr UserContext;

        [StructLayout(LayoutKind.Explicit)]
        public struct ETW_BUFFER_CONTEXT
        {
            [FieldOffset(0)] public byte ProcessorNumber;

            [FieldOffset(1)] public byte Alignment;

            [FieldOffset(0)] public UInt16 ProcessorIndex;

            [FieldOffset(2)] public UInt16 LoggerId;
        }
    }

    [Serializable]
    internal enum MAP_FLAGS
    {
        EVENTMAP_INFO_FLAG_MANIFEST_VALUEMAP,
        EVENTMAP_INFO_FLAG_MANIFEST_BITMAP,
        EVENTMAP_INFO_FLAG_MANIFEST_PATTERNMAP,
        EVENTMAP_INFO_FLAG_WBEM_VALUEMAP,
        EVENTMAP_INFO_FLAG_WBEM_BITMAP,
        EVENTMAP_INFO_FLAG_WBEM_FLAG,
        EVENTMAP_INFO_FLAG_WBEM_NO_MAP
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]

    internal struct EVENT_MAP_ENTRY
    {
        [FieldOffset(0)] public uint OutputOffset;
        [FieldOffset(4)] public uint Value;
        [FieldOffset(4)] public uint InputOffset;
    };

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct EVENT_MAP_INFO
    {
        public uint NameOffset;
        public MAP_FLAGS Flag;
        public uint EntryCount;
        public uint FormatStringOffset; // This should be union
    };

    /// <summary>
    /// EventTraceHeader structure used by EVENT_TRACE_PROPERTIES
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WNODE_HEADER
    {
        public UInt32 BufferSize;
        public UInt32 ProviderId;
        public UInt64 HistoricalContext;
        public UInt64 TimeStamp;
        public Guid Guid;
        public UInt32 ClientContext;  // Determines the time stamp resolution
        public UInt32 Flags;
    }

    /// <summary>
    /// EVENT_TRACE_PROPERTIES is a structure used by StartTrace, ControlTrace
    /// however it can not be used directly in the definition of these functions
    /// because extra information has to be hung off the end of the structure
    /// before being passed.  (LofFileNameOffset, LoggerNameOffset)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct EVENT_TRACE_PROPERTIES
    {
        public WNODE_HEADER Wnode;      // Timer Resolution determined by the Wnode.ClientContext.  
        public UInt32 BufferSize;
        public UInt32 MinimumBuffers;
        public UInt32 MaximumBuffers;
        public UInt32 MaximumFileSize;
        public UInt32 LogFileMode;
        public UInt32 FlushTimer;
        public UInt32 EnableFlags;
        public Int32 AgeLimit;
        public UInt32 NumberOfBuffers;
        public UInt32 FreeBuffers;
        public UInt32 EventsLost;
        public UInt32 BuffersWritten;
        public UInt32 LogBuffersLost;
        public UInt32 RealTimeBuffersLost;
        public IntPtr LoggerThreadId;
        public UInt32 LogFileNameOffset;
        public UInt32 LoggerNameOffset;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct EVENT_FILTER_DESCRIPTOR
    {
        [FieldOffset(0)]
        public byte* Ptr;          // Data
        [FieldOffset(8)]
        public int Size;
        [FieldOffset(12)]
        public int Type;        // Can be user defined, but also the EVENT_FILTER_TYPE* constants above.  
    };

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ENABLE_TRACE_PARAMETERS
    {
        public uint Version;
        public uint EnableProperty;
        public uint ControlFlags;
        public Guid SourceId;
        public EVENT_FILTER_DESCRIPTOR* EnableFilterDesc;
        public int FilterDescCount;        // according to docs Win7 should have it although PerfView says it's for Win 8.1+
    };

    internal enum TRACE_INFO_CLASS
    {
        TraceGuidQueryList,                     // Get Guids of all providers registered on the computer
        TraceGuidQueryInfo,                     // Query information that each session a particular provider.  
        TraceGuidQueryProcess,                  // Query an array of GUIDs of the providers that registered themselves in the same process as the calling process
        TraceStackTracingInfo,                  // This is the last one supported on Win7
                                                // Win 8 
        TraceSystemTraceEnableFlagsInfo,        // Turns on kernel event logger
        TraceSampledProfileIntervalInfo,        // TRACE_PROFILE_INTERVAL (allows you to set the sampling interval) (Set, Get)

        TraceProfileSourceConfigInfo,           // int array, turns on all listed sources.  (Set)
        TraceProfileSourceListInfo,             // PROFILE_SOURCE_INFO linked list (converts names to source numbers) (Get)

        // Used to collect extra info on other events (currently only context switch).  
        TracePmcEventListInfo,                  // CLASSIC_EVENT_ID array (Works like TraceStackTracingInfo)
        TracePmcCounterListInfo,                // int array
        MaxTraceSetInfoClass
    };

	public struct STACK_TRACING_EVENT_ID
	{
		public Guid EventGuid;

		public byte Type;

		private byte Reserved1;
		private byte Reserved2;
		private byte Reserved3;
		private byte Reserved4;
		private byte Reserved5;
		private byte Reserved6;
		private byte Reserved7;
	}
}