using System;
using System.Collections.Generic;
using System.Text;

namespace Tx.Windows.Parsers
{
    // FIXME only temporary - must be filled with valid properties
    public class KernelTraceEventParser
    {

        /// <summary>
        /// This is passed to EtwRecorder.EnableKernelProvider to enable particular sets of
        /// events.  See https://docs.microsoft.com/en-us/windows/win32/api/evntrace/ns-evntrace-event_trace_properties_v2#members 
        /// for more information on them 
        /// </summary>
        [Flags]
        public enum Keywords
        {
            /// <summary>
            /// Logs nothing
            /// </summary>
            None = 0x00000000, // no tracing
                               // Part of the 'default set of keywords' (good value in most scenarios).  
            /// <summary>
            /// Logs the mapping of file IDs to actual (kernel) file names. 
            /// </summary>
            DiskFileIO = 0x00000200,
            /// <summary>
            /// Loads the completion of Physical disk activity. 
            /// </summary>
            DiskIO = 0x00000100, // physical disk IO
            /// <summary>
            /// Logs native modules loads (LoadLibrary), and unloads
            /// </summary>
            ImageLoad = 0x00000004, // image load
            /// <summary>
            /// Logs all page faults that must fetch the data from the disk (hard faults)
            /// </summary>
            MemoryHardFaults = 0x00002000,
            /// <summary>
            /// Logs TCP/IP network send and receive events. 
            /// </summary>
            NetworkTCPIP = 0x00010000,
            /// <summary>
            /// Logs process starts and stops.
            /// </summary>
            Process = 0x00000001,
            /// <summary>
            /// Logs process performance counters (TODO When?) (Vista+ only)
            /// see KernelTraceEventParser.ProcessPerfCtr, ProcessPerfCtrTraceData
            /// </summary>
            ProcessCounters = 0x00000008,
            /// <summary>
            /// Sampled based profiling (every msec) (Vista+ only) (expect 1K events per proc per second)
            /// </summary>
            Profile = 0x01000000,
            /// <summary>
            /// Logs threads starts and stops
            /// </summary>
            Thread = 0x00000002,

            // These are useful in some situations, however are more volumous so are not part of the default set. 
            /// <summary>
            /// log thread context switches (Vista only) (can be > 10K events per second)
            /// </summary>
            ContextSwitch = 0x00000010,
            /// <summary>
            /// log Disk operations (Vista+ only)
            /// Generally not TOO volumous (typically less than 1K per second) (Stacks associated with this)
            /// </summary>
            DiskIOInit = 0x00000400,
            /// <summary>
            /// Thread Dispatcher (ReadyThread) (Vista+ only) (can be > 10K events per second)
            /// </summary>
            Dispatcher = 0x00000800,
            /// <summary>
            /// log file FileOperationEnd (has status code) when they complete (even ones that do not actually
            /// cause Disk I/O).  (Vista+ only)
            /// Generally not TOO volumous (typically less than 1K per second) (No stacks associated with these)
            /// </summary>
            FileIO = 0x02000000,
            /// <summary>
            /// log the start of the File I/O operation as well as the end. (Vista+ only)
            /// Generally not TOO volumous (typically less than 1K per second)
            /// </summary>
            FileIOInit = 0x04000000,
            /// <summary>
            /// Logs all page faults (hard or soft)
            /// Can be pretty volumous (> 1K per second)
            /// </summary>
            Memory = 0x00001000,
            /// <summary>
            /// Logs activity to the windows registry. 
            /// Can be pretty volumous (> 1K per second)
            /// </summary>
            Registry = 0x00020000, // registry calls
            /// <summary>
            /// log calls to the OS (Vista+ only)
            /// This is VERY volumous (can be > 100K events per second)
            /// </summary>
            SystemCall = 0x00000080,
            /// <summary>
            /// Log Virtual Alloc calls and VirtualFree.   (Vista+ Only)
            /// Generally not TOO volumous (typically less than 1K per second)
            /// </summary> 
            VirtualAlloc = 0x004000,
            /// <summary>
            /// Log mapping of files into memory (Win8 and above Only)
            /// Generally low volume.  
            /// </summary>
            VAMap = 0x8000,

            // advanced logging (when you care about the internals of the OS)
            /// <summary>
            /// Logs Advanced Local Procedure call events. 
            /// </summary>
            AdvancedLocalProcedureCalls = 0x00100000,
            /// <summary>
            /// log defered procedure calls (an Kernel mechanism for having work done asynchronously) (Vista+ only)
            /// </summary> 
            DeferedProcedureCalls = 0x00000020,
            /// <summary>
            /// Device Driver logging (Vista+ only)
            /// </summary>
            Driver = 0x00800000,
            /// <summary>
            /// log hardware interrupts. (Vista+ only)
            /// </summary>
            Interrupt = 0x00000040,
            /// <summary>
            /// Disk I/O that was split (eg because of mirroring requirements) (Vista+ only)
            /// </summary> 
            SplitIO = 0x00200000,
            /// <summary>
            /// Good default kernel flags.  (TODO more detail)
            /// </summary>  
            Default = DiskIO | DiskFileIO | DiskIOInit | ImageLoad | MemoryHardFaults | NetworkTCPIP | Process | ProcessCounters | Profile | Thread,
            /// <summary>
            /// These events are too verbose for normal use, but this give you a quick way of turing on 'interesting' events
            /// This does not include SystemCall because it is 'too verbose'
            /// </summary>
            Verbose = Default | ContextSwitch | Dispatcher | FileIO | FileIOInit | Memory | Registry | VirtualAlloc | VAMap,  // use as needed
            /// <summary>
            /// Use this if you care about blocked time.  
            /// </summary>
            ThreadTime = Default | ContextSwitch | Dispatcher,
            /// <summary>
            /// You mostly don't care about these unless you are dealing with OS internals.  
            /// </summary>
            OS = AdvancedLocalProcedureCalls | DeferedProcedureCalls | Driver | Interrupt | SplitIO,
            /// <summary>
            /// All legal kernel events
            /// </summary>
            All = Verbose | ContextSwitch | Dispatcher | FileIO | FileIOInit | Memory | Registry | VirtualAlloc | VAMap  // use as needed
                | SystemCall        // Interesting but very expensive. 
                | OS,

            /// <summary>
            /// These are the kernel events that are not allowed in containers.  Can be subtracted out.  
            /// </summary>
            NonContainer = ~(Process | Thread | ImageLoad | Profile | ContextSwitch | ProcessCounters),

            // These are ones that I have made up  
            // All = 0x07B3FFFF, so 4'0000, 8'0000, 40'0000, and F000'00000 are free.  
            /// <summary>
            /// Turn on PMC (Precise Machine Counter) events.   Only Win 8
            /// </summary>
            PMCProfile = unchecked((int)0x80000000),
            /// <summary>
            /// Kernel reference set events (like XPERF ReferenceSet).   Fully works only on Win 8.  
            /// </summary>
            ReferenceSet = 0x40000000,
            /// <summary>
            /// Events when thread priorities change.  
            /// </summary>
            ThreadPriority = 0x20000000,
            /// <summary>
            /// Events when queuing and dequeuing from the I/O completion ports.    
            /// </summary>
            IOQueue = 0x10000000,
            /// <summary>
            /// Handle creation and closing (for handle leaks) 
            /// </summary>
            Handle = 0x400000,
        };

        /// <summary>
        /// Given a mask of kernel flags, set the array stackTracingIds of size stackTracingIdsMax to match.
        /// It returns the number of entries in stackTracingIds that were filled in.
        /// </summary>
        internal static unsafe int SetStackTraceIds(Keywords stackCapture, STACK_TRACING_EVENT_ID* stackTracingIds, int stackTracingIdsMax)
        {
            int curID = 0;

            // PerfInfo (sample profiling)
            if ((stackCapture & KernelTraceEventParser.Keywords.Profile) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PerfInfoTaskGuid;
                stackTracingIds[curID].Type = 0x2e;     // Sample Profile
                curID++;
            }

            // PCM sample profiling
            if ((stackCapture & KernelTraceEventParser.Keywords.PMCProfile) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PerfInfoTaskGuid;
                stackTracingIds[curID].Type = 0x2f;     // PMC Sample Profile
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.SystemCall) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PerfInfoTaskGuid;
                // stackTracingIds[curID].Type = 0x33;     // SysCallEnter
                stackTracingIds[curID].Type = 0x34;     // SysCallExit  (We want the stack on the exit as it has the return value).  
                curID++;
            }
            // Thread
            if ((stackCapture & KernelTraceEventParser.Keywords.Thread) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x01;     // Thread Create
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.ContextSwitch) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x24;     // Context Switch
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.ThreadPriority) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x30;     // Set Priority
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x31;     // Set Base Priority
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.Dispatcher) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x32;     // Ready Thread
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.IOQueue) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x3e;     // #define PERFINFO_LOG_TYPE_KQUEUE_ENQUEUE            (EVENT_TRACE_GROUP_THREAD | 0x3E) 
                curID++;
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x3f;     // #define PERFINFO_LOG_TYPE_KQUEUE_DEQUEUE            (EVENT_TRACE_GROUP_THREAD | 0x3F) 
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.Handle) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ObjectTaskGuid;
                stackTracingIds[curID].Type = 0x20;     // PERFINFO_LOG_TYPE_CREATE_HANDLE                (EVENT_TRACE_GROUP_OBJECT | 0x20)  
                curID++;
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ObjectTaskGuid;
                stackTracingIds[curID].Type = 0x21;     // PERFINFO_LOG_TYPE_CLOSE_HANDLE                 (EVENT_TRACE_GROUP_OBJECT | 0x21)   
                curID++;
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ObjectTaskGuid;
                stackTracingIds[curID].Type = 0x22;     // PERFINFO_LOG_TYPE_DUPLICATE_HANDLE             (EVENT_TRACE_GROUP_OBJECT | 0x22)   
                curID++;
            }

            // Image
            if ((stackCapture & KernelTraceEventParser.Keywords.ImageLoad) != 0)
            {
                // Confirm this is not ImageTaskGuid
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ProcessTaskGuid;
                stackTracingIds[curID].Type = 0x0A;     // EVENT_TRACE_TYPE_LOAD (Image Load)
                curID++;
            }

            // Process
            if ((stackCapture & KernelTraceEventParser.Keywords.Process) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ProcessTaskGuid;
                stackTracingIds[curID].Type = 0x01;        // Process Create
                stackTracingIds[curID].Type = 0x0B;        // EVENT_TRACE_TYPE_TERMINATE   
                curID++;
            }

            // Disk
            if ((stackCapture & KernelTraceEventParser.Keywords.DiskIOInit) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.DiskIOTaskGuid;
                stackTracingIds[curID].Type = 0x0c;     // Read Init
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.DiskIOTaskGuid;
                stackTracingIds[curID].Type = 0x0d;     // Write Init
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.DiskIOTaskGuid;
                stackTracingIds[curID].Type = 0x0f;     // Flush Init
                curID++;
            }

            // Virtual Alloc
            if ((stackCapture & (KernelTraceEventParser.Keywords.VirtualAlloc | KernelTraceEventParser.Keywords.ReferenceSet)) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.VirtualAllocTaskGuid;
                stackTracingIds[curID].Type = 0x62;     // Flush Init
                curID++;
            }

            // VAMap 
            if ((stackCapture & KernelTraceEventParser.Keywords.VAMap) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIOTaskGuid;
                stackTracingIds[curID].Type = 0x25;
                curID++;
            }

            // Hard Faults
            if ((stackCapture & KernelTraceEventParser.Keywords.MemoryHardFaults) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x20;     // Hard Fault
                curID++;
            }

            // Page Faults 
            if ((stackCapture & KernelTraceEventParser.Keywords.Memory) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x0A;     // Transition Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x0B;     // Demand zero Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x0C;     // Copy on Write Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x0D;     // Guard Page Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x0E;     // Hard Page Fault
                curID++;

                // Unconditionally turn on stack capture for Access Violations.  
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x0F;     // (access Violation) EVENT_TRACE_TYPE_MM_AV
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.ReferenceSet) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x49;     //  PERFINFO_LOG_TYPE_PFMAPPED_SECTION_CREATE 
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x4F;     // PERFINFO_LOG_TYPE_PFMAPPED_SECTION_DELETE
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x76;     // PERFINFO_LOG_TYPE_PAGE_ACCESS 
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x77;     // PERFINFO_LOG_TYPE_PAGE_RELEASE
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x78;     // PERFINFO_LOG_TYPE_PAGE_RANGE_ACCESS 
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x79;     // PERFINFO_LOG_TYPE_PAGE_RANGE_RELEASE 
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x82;     // PERFINFO_LOG_TYPE_PAGE_ACCESS_EX 
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.MemoryTaskGuid;
                stackTracingIds[curID].Type = 0x83;     // PERFINFO_LOG_TYPE_REMOVEFROMWS 
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.FileIOInit) != 0)
            {
                // TODO allow stacks only on open and close;
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIOTaskGuid;
                stackTracingIds[curID].Type = 0x40;     // Create
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIOTaskGuid;
                stackTracingIds[curID].Type = 0x41;     // Cleanup
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIOTaskGuid;
                stackTracingIds[curID].Type = 0x42;     // Close
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIOTaskGuid;
                stackTracingIds[curID].Type = 0x43;     // Read
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIOTaskGuid;
                stackTracingIds[curID].Type = 0x44;     // Write
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.Registry) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0A;     // NtCreateKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0B;     // NtOpenKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0C;     // NtDeleteKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0D;     // NtQueryKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0E;     // NtSetValueKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0F;     // NtDeleteValueKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x10;     // NtQueryValueKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x11;     // NtEnumerateKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x12;     // NtEnumerateValueKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x13;     // NtQueryMultipleValueKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x14;     // NtSetInformationKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x15;     // NtFlushKey
                curID++;

                // TODO What are these?  
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x16;     // KcbCreate
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x17;     // KcbDelete
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x1A;     // VirtualizeKey
                curID++;
            }

            // ALPC
            if ((stackCapture & KernelTraceEventParser.Keywords.AdvancedLocalProcedureCalls) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ALPCTaskGuid;
                stackTracingIds[curID].Type = 33;  // send message   
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ALPCTaskGuid;
                stackTracingIds[curID].Type = 34;  // receive message   
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ALPCTaskGuid;
                stackTracingIds[curID].Type = 35;  // wait for reply
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ALPCTaskGuid;
                stackTracingIds[curID].Type = 36;  // wait for new message
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ALPCTaskGuid;
                stackTracingIds[curID].Type = 37;  // unwait
                curID++;
            }

            // Confirm we did not overflow.  
            Debug.Assert(curID <= stackTracingIdsMax);
            return curID;
        }

        internal static readonly Guid EventTraceTaskGuid = new Guid(unchecked((int)0x68fdd900), unchecked((short)0x4a3e), unchecked((short)0x11d1), 0x84, 0xf4, 0x00, 0x00, 0xf8, 0x04, 0x64, 0xe3);
        internal static readonly Guid ProcessTaskGuid = new Guid(unchecked((int)0x3d6fa8d0), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        internal static readonly Guid ThreadTaskGuid = new Guid(unchecked((int)0x3d6fa8d1), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        internal static readonly Guid DiskIOTaskGuid = new Guid(unchecked((int)0x3d6fa8d4), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        internal static readonly Guid RegistryTaskGuid = new Guid(unchecked((int)0xae53722e), unchecked((short)0xc863), unchecked((short)0x11d2), 0x86, 0x59, 0x00, 0xc0, 0x4f, 0xa3, 0x21, 0xa1);
        internal static readonly Guid SplitIoTaskGuid = new Guid(unchecked((int)0xd837ca92), unchecked((short)0x12b9), unchecked((short)0x44a5), 0xad, 0x6a, 0x3a, 0x65, 0xb3, 0x57, 0x8a, 0xa8);
        internal static readonly Guid FileIOTaskGuid = new Guid(unchecked((int)0x90cbdc39), unchecked((short)0x4a3e), unchecked((short)0x11d1), 0x84, 0xf4, 0x00, 0x00, 0xf8, 0x04, 0x64, 0xe3);
        internal static readonly Guid TcpIpTaskGuid = new Guid(unchecked((int)0x9a280ac0), unchecked((short)0xc8e0), unchecked((short)0x11d1), 0x84, 0xe2, 0x00, 0xc0, 0x4f, 0xb9, 0x98, 0xa2);
        internal static readonly Guid UdpIpTaskGuid = new Guid(unchecked((int)0xbf3a50c5), unchecked((short)0xa9c9), unchecked((short)0x4988), 0xa0, 0x05, 0x2d, 0xf0, 0xb7, 0xc8, 0x0f, 0x80);
        internal static readonly Guid ImageTaskGuid = new Guid(unchecked((int)0x2cb15d1d), unchecked((short)0x5fc1), unchecked((short)0x11d2), 0xab, 0xe1, 0x00, 0xa0, 0xc9, 0x11, 0xf5, 0x18);
        internal static readonly Guid MemoryTaskGuid = new Guid(unchecked((int)0x3d6fa8d3), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        internal static readonly Guid MemoryProviderGuid = new Guid(unchecked((int)0x3d1d93ef7), unchecked((short)0xe1f2), unchecked((short)0x4f45), 0x99, 0x43, 0x03, 0xd2, 0x45, 0xfe, 0x6c, 0x00);
        internal static readonly Guid PerfInfoTaskGuid = new Guid(unchecked((int)0xce1dbfb4), unchecked((short)0x137e), unchecked((short)0x4da6), 0x87, 0xb0, 0x3f, 0x59, 0xaa, 0x10, 0x2c, 0xbc);
        internal static readonly Guid StackWalkTaskGuid = new Guid(unchecked((int)0xdef2fe46), unchecked((short)0x7bd6), unchecked((short)0x4b80), 0xbd, 0x94, 0xf5, 0x7f, 0xe2, 0x0d, 0x0c, 0xe3);
        // Used for new style user mode stacks.  
        internal static readonly Guid EventTracingProviderGuid = new Guid(unchecked((int)0xb675ec37), unchecked((short)0xbdb6), unchecked((short)0x4648), 0xbc, 0x92, 0xf3, 0xfd, 0xc7, 0x4d, 0x3c, 0xa2);
        internal static readonly Guid ALPCTaskGuid = new Guid(unchecked((int)0x45d8cccd), unchecked((short)0x539f), unchecked((short)0x4b72), 0xa8, 0xb7, 0x5c, 0x68, 0x31, 0x42, 0x60, 0x9a);
        internal static readonly Guid LostEventTaskGuid = new Guid(unchecked((int)0x6a399ae0), unchecked((short)0x4bc6), unchecked((short)0x4de9), 0x87, 0x0b, 0x36, 0x57, 0xf8, 0x94, 0x7e, 0x7e);
        internal static readonly Guid SystemConfigTaskGuid = new Guid(unchecked((int)0x01853a65), unchecked((short)0x418f), unchecked((short)0x4f36), 0xae, 0xfc, 0xdc, 0x0f, 0x1d, 0x2f, 0xd2, 0x35);
        internal static readonly Guid VirtualAllocTaskGuid = new Guid(unchecked((int)0x3d6fa8d3), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        internal static readonly Guid ReadyThreadTaskGuid = new Guid(unchecked((int)0x3d6fa8d1), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        internal static readonly Guid SysConfigTaskGuid = new Guid(unchecked((int)0x9b79ee91), unchecked((short)0xb5fd), 0x41c0, 0xa2, 0x43, 0x42, 0x48, 0xe2, 0x66, 0xe9, 0xD0);
        internal static readonly Guid ObjectTaskGuid = new Guid(unchecked((int)0x89497f50), unchecked((short)0xeffe), 0x4440, 0x8c, 0xf2, 0xce, 0x6b, 0x1c, 0xdc, 0xac, 0xa7);
    }
}
