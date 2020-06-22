using System;
using System.Collections.Generic;
using System.Text;

namespace Tx.Windows.Etw
{
    public enum FileLoggingMode
    {
        Circular,
        MultiFile,
        SingleFile
    }

    public sealed class EtwSessionConfig
    {
        public EtwSessionConfig(string name, string fileName = null, uint minBufferNumber = 0,
            uint bufferSizeKB = 0, FileLoggingMode loggingMode = FileLoggingMode.SingleFile, uint maxFileSizeMB = 0)
        {
            Name = name;
            FileName = fileName;
            MinBufferNumber = minBufferNumber == 0 ? (uint)(Environment.ProcessorCount * 2) : minBufferNumber;
            BufferSizeKB = bufferSizeKB == 0 ? 64 : bufferSizeKB;
            FileMode = loggingMode;
            MaxFileSizeMB = maxFileSizeMB;
        }

        public string Name { get; }

        public uint MinBufferNumber { get; }

        public uint BufferSizeKB { get; }

        public string FileName { get; }

        public FileLoggingMode FileMode { get; }

        public uint MaxFileSizeMB { get; }

        public bool IsRealTime { get { return string.IsNullOrEmpty(FileName); } }
    }

    public sealed class EtwEventsFilter
    {
        private static readonly int[] emptyIntArray = new int[0];
        private static readonly string[] emptyStringArray = new string[0];

        public EtwEventsFilter(IEnumerable<int> allowedProcessIds = null,
            IEnumerable<string> allowedProcessNames = null)
        {
            AllowedProcessIds = allowedProcessIds ?? emptyIntArray;
            AllowedProcessNames = allowedProcessNames ?? emptyStringArray;
        }

        public IEnumerable<int> AllowedProcessIds { get; }

        public IEnumerable<string> AllowedProcessNames { get; }

        // TODO: other filters available but I'm not implementing them now 
        // (check TraceEventProviderOptions class in TraceEvent to know more)
    }

    public sealed class EtwProviderSessionConfig
    {
        public EtwProviderSessionConfig(Guid providerId, TraceEventLevel level = TraceEventLevel.Verbose, 
            ulong matchAnyKeywords = ulong.MaxValue, ulong matchAllKeywords = ulong.MaxValue,
            bool stacksEnabled = false)
        {
            ProviderId = providerId;
            ProviderLevel = level;
            MatchAnyKeywords = matchAnyKeywords;
            MatchAllKeywords = matchAllKeywords;
            StacksEnabled = stacksEnabled;
        }

        public Guid ProviderId { get; }

        public TraceEventLevel ProviderLevel { get; }

        public ulong MatchAnyKeywords { get; }

        public ulong MatchAllKeywords { get; }

        public bool StacksEnabled { get; }
    }

    public sealed class EtwSession
    {
        internal EtwSession(ulong traceHandle, EtwSessionConfig config)
        {
            TraceHandle = traceHandle;
            Config = config;
        }

        public ulong TraceHandle { get; }

        public string Name { get { return Config.Name; } }

        public EtwSessionConfig Config { get; }
    }

    /// <summary>
    /// There are certain classes of events (like start and stop) which are common across a broad variety of
    /// event providers for which it is useful to treat uniformly (for example, determining the elapsed time
    /// between a start and stop event).  To facilitate this, event can have opcode which defines these
    /// common operations.  Below are the standard ones but providers can define additional ones.
    /// </summary>
    public enum TraceEventOpcode : byte
    {
        /// <summary>
        /// Generic opcode that does not have specific semantics associated with it. 
        /// </summary>
        Info = 0,
        /// <summary>
        /// The entity (process, thread, ...) is starting
        /// </summary>
        Start = 1,
        /// <summary>
        /// The entity (process, thread, ...) is stoping (ending)
        /// </summary>
        Stop = 2,
        /// <summary>
        /// The entity (process, thread, ...) did not terminate before data collection ended, so indicate
        /// this at data collection termination time.
        /// </summary>
        DataCollectionStart = 3,
        /// <summary>
        /// The entity (process, thread, ...) did not terminate before data collection ended, so indicate
        /// this at data collection termination time. This is mostly for 'flight recorder' scenarios where
        /// you only have the 'tail' of the data and would like to know about everything that existed. 
        /// </summary>
        DataCollectionStop = 4,
        /// <summary>
        /// Reserved
        /// </summary>
        Extension = 5,
        /// <summary>
        /// Reserved
        /// </summary>
        Reply = 6,
        /// <summary>
        /// Reserved
        /// </summary>
        Resume = 7,
        /// <summary>
        /// Reserved
        /// </summary>
        Suspend = 8,
        /// <summary>
        /// Reserved
        /// </summary>
        Transfer = 9,
        // Receive = 240,
        // 255 is used as in 'illegal opcode' and signifies a WPP style event.  These events 
        // use the event ID and the TASK Guid as their lookup key.  
    };

    /// <summary>
    /// Indicates to a provider whether verbose events should be logged.  
    /// </summary>
    public enum TraceEventLevel
    {
        /// <summary>
        /// Always log the event (It also can mean that the provider decides the verbosity)  You probably should not use it....
        /// </summary>
        Always = 0,
        /// <summary>
        /// Events that indicate critical conditions
        /// </summary>
        Critical = 1,
        /// <summary>
        /// Events that indicate error conditions
        /// </summary>
        Error = 2,
        /// <summary>
        /// Events that indicate warning conditions
        /// </summary>
        Warning = 3,
        /// <summary>
        /// Events that indicate information
        /// </summary>
        Informational = 4,
        /// <summary>
        /// Events that verbose information
        /// </summary>
        Verbose = 5,
    };

    /// <summary>
    /// ETW defines the concept of a Keyword, which is a 64 bit bitfield. Each bit in the bitfield
    /// represents some provider defined 'area' that is useful for filtering. When processing the events, it
    /// is then possible to filter based on whether various bits in the bitfield are set.  There are some
    /// standard keywords, but most are provider specific. 
    /// </summary>
    [Flags]
    public enum TraceEventKeyword : long
    {
        /// <summary>
        /// No event groups (keywords) selected
        /// </summary>
        None = 0L,

        /* The top 16 bits are reserved for system use (TODO define them) */

        /// <summary>
        /// All event groups (keywords) selected
        /// </summary>
        All = -1,
    }
}
