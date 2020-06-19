using System;
using System.Diagnostics;

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

    public sealed class EtwSession
    {
        public EtwSession(ulong traceHandle, EtwSessionConfig config)
        {
            TraceHandle = traceHandle;
            Config = config;
        }

        public ulong TraceHandle { get; }

        public EtwSessionConfig Config { get; }
    }

    public static unsafe class EtwRecorder
    {
        internal const int MaxNameSize = 1024;
        private const int MaxExtensionSize = 256;
        private static readonly int PropertiesSize = sizeof(EVENT_TRACE_PROPERTIES) + 2 * MaxNameSize * sizeof(char) + MaxExtensionSize;

        public static EtwSession StartSession(string name, string fileName = null)
        {
            var config = new EtwSessionConfig(name, fileName);

            var buffer = stackalloc byte[PropertiesSize];
            var properties = GetProperties(buffer, config);
            EtwNativeMethods.StartTraceW(out var sessionHandle, name, properties).CheckError();

            return new EtwSession(sessionHandle, config);
        }

        public static void StopSession(EtwSession session)
        {
            var buffer = stackalloc byte[PropertiesSize];
            var properties = GetProperties(buffer, session.Config);
            EtwNativeMethods.ControlTrace(0UL, session.Config.Name, properties, 
                EtwNativeConstants.EVENT_TRACE_CONTROL_STOP).CheckError();
        }

        /// <summary>
        /// Get a EVENT_TRACE_PROPERTIES structure suitable for passing the the ETW out of a 'buffer' which must be PropertiesSize bytes
        /// in size.
        /// </summary>
        private static EVENT_TRACE_PROPERTIES* GetProperties(byte* buffer, EtwSessionConfig config)
        {
            WinApiNativeMethods.ZeroMemory(buffer, PropertiesSize);
            var properties = (EVENT_TRACE_PROPERTIES*)buffer;

            properties->LoggerNameOffset = (uint)sizeof(EVENT_TRACE_PROPERTIES);

            // Copy in the session name
            if (config.Name.Length > MaxNameSize - 1)
            {
                throw new ArgumentException("File name too long", "sessionName");
            }

            char* sessionNamePtr = (char*)(((byte*)properties) + properties->LoggerNameOffset);
            CopyStringToPtr(sessionNamePtr, config.Name);

            properties->Wnode.BufferSize = (uint)PropertiesSize;
            properties->Wnode.Flags = EtwNativeConstants.WNODE_FLAG_TRACED_GUID;
            properties->FlushTimer = 60;                // flush every minute for file based collection.  

            Debug.Assert(config.BufferSizeKB != 0);
            properties->BufferSize = config.BufferSizeKB;
            // FIXME: in perfview they make if much higher (on my machine it's 1024 buffers)
            properties->MinimumBuffers = config.MinBufferNumber;
            properties->LogFileMode = EtwNativeConstants.EVENT_TRACE_INDEPENDENT_SESSION_MODE;

            // TODO: it is possible to enable both real-time and file based sessions
            // so something to think about in the future

            // LoggingModes are explained here: https://docs.microsoft.com/en-us/windows/win32/etw/logging-mode-constants
            properties->LogFileMode = EtwNativeConstants.EVENT_TRACE_INDEPENDENT_SESSION_MODE;
            if (config.IsRealTime)
            {
                properties->FlushTimer = 1;              // flush every second (as fast as possible) for real time. 
                properties->LogFileMode |= EtwNativeConstants.EVENT_TRACE_REAL_TIME_MODE;
                properties->LogFileNameOffset = 0;
            }
            else
            {
                Debug.Assert(!string.IsNullOrEmpty(config.FileName));
                switch (config.FileMode)
                {
                    case FileLoggingMode.Circular:
                        properties->LogFileMode |= EtwNativeConstants.EVENT_TRACE_FILE_MODE_CIRCULAR;
                        Debug.Assert(config.MaxFileSizeMB > 0);
                        properties->MaximumFileSize = config.MaxFileSizeMB;
                        break;
                    case FileLoggingMode.MultiFile:
                        properties->LogFileMode |= EtwNativeConstants.EVENT_TRACE_FILE_MODE_NEWFILE;
                        Debug.Assert(config.MaxFileSizeMB > 0);
                        properties->MaximumFileSize = config.MaxFileSizeMB;
                        break;
                    default:
                        properties->LogFileMode |= EtwNativeConstants.EVENT_TRACE_FILE_MODE_SEQUENTIAL;
                        break;
                }

                if (config.FileName.Length > MaxNameSize - 1)
                {
                    throw new ArgumentException("File name too long", "fileName");
                }

                char* fileNamePtr = (char*)(((byte*)properties) + properties->LogFileNameOffset);
                CopyStringToPtr(fileNamePtr, config.FileName);
                properties->LogFileNameOffset = properties->LoggerNameOffset + MaxNameSize * sizeof(char);
            }

            properties->MaximumBuffers = properties->MinimumBuffers * 5 / 4 + 10;

            properties->Wnode.ClientContext = 1;    // set Timer resolution to 100ns.  
            return properties;
        }

        private static unsafe void CopyStringToPtr(char* toPtr, string str)
        {
            fixed (char* fromPtr = str)
            {
                int i = 0;
                while (i < str.Length)
                {
                    toPtr[i] = fromPtr[i];
                    i++;
                }
                toPtr[i] = '\0';   // Null terminate
            }
        }
    }
}
