using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Diagnostics;
using Tx.Windows.Parsers;
using Keywords = Tx.Windows.Parsers.KernelTraceEventParser.Keywords;

namespace Tx.Windows.Etw
{
    public static unsafe class EtwRecorder
    {
        /// <summary>
        /// The special name for the Kernel session
        /// </summary>
        private const string KernelSessionName = "NT Kernel Logger";

        public static readonly string ProviderName = "Windows Kernel";
        public static readonly Guid SystemTraceControlGuid = new Guid(unchecked((int)0x9e814aad), unchecked((short)0x3204), unchecked((short)0x11d2), 0x9a, 0x82, 0x00, 0x60, 0x08, 0xa8, 0x69, 0x39);

        internal const int MaxNameSize = 1024;
        private const int MaxExtensionSize = 256;
        private static readonly int PropertiesSize = sizeof(EVENT_TRACE_PROPERTIES) + 2 * MaxNameSize * sizeof(char) + MaxExtensionSize;

        public static EtwSession StartSession(string name, string fileName = null)
        {
            var config = new EtwSessionConfig(name, fileName);

            var buffer = stackalloc byte[PropertiesSize];
            var properties = GetProperties(buffer, config);
            EtwNativeMethods.StartTraceW(out var sessionHandle, name, properties).CheckWinError();

            return new EtwSession(sessionHandle, config);
        }

        public static EtwSession StartNtKernelSession(Keywords flags, string fileName = null)
        {
            if (flags != Keywords.None)
            {
                flags |= (Keywords.Process | Keywords.Thread);
            }

            var config = new EtwSessionConfig(KernelSessionName, fileName);

            var buffer = stackalloc byte[PropertiesSize];
            var properties = GetProperties(buffer, config);

            properties->Wnode.Guid = SystemTraceControlGuid;
            properties->EnableFlags = (uint)flags;

            // FIXME: what about stacks?
            EtwNativeMethods.StartTraceW(out var sessionHandle, KernelSessionName, properties).CheckWinError();

            return new EtwSession(sessionHandle, config);
        }

        public static void StopSession(EtwSession session)
        {
            var buffer = stackalloc byte[PropertiesSize];
            var properties = GetProperties(buffer, session.Config);
            EtwNativeMethods.ControlTrace(0UL, session.Config.Name, properties,
                EtwNativeConstants.EVENT_TRACE_CONTROL_STOP).CheckWinError();
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

        public static void EnableKernelProvider(EtwSession session, Keywords flags,
            Keywords stackCapture = Keywords.None)
        {
            // FIXME: container fix may be required (check EnableKernelProvider in Perfview)

            // many of the kernel events are missing the process or thread information and have to be fixed up.  In order to do this I need the
            // process and thread events to do this, so we turn those on if any other keyword is on.  
            if (flags != Keywords.None)
            {
                flags |= (Keywords.Process | Keywords.Thread);
            }

            // FIXME the code below will need to be added
            // The Profile event requires the SeSystemProfilePrivilege to succeed, so set it.  
            //if ((flags & (KernelTraceEventParser.Keywords.Profile | KernelTraceEventParser.Keywords.PMCProfile)) != 0)
            //{
            //    TraceEventNativeMethods.SetPrivilege(TraceEventNativeMethods.SE_SYSTEM_PROFILE_PRIVILEGE);
            //    double cpu100ns = (CpuSampleIntervalMSec * 10000.0 + .5);
            //    // The API seems to have an upper bound of 1 second.  
            //    if (cpu100ns >= int.MaxValue || ((int)cpu100ns) > 10000000)
            //    {
            //        throw new ApplicationException("CPU Sampling interval is too large.");
            //    }

            //    var succeeded = ETWControl.SetCpuSamplingRate((int)cpu100ns);       // Always try to set, since it may not be the default
            //    if (!succeeded && CpuSampleIntervalMSec != 1.0F)
            //    {
            //        throw new ApplicationException("Can't set CPU sampling to " + CpuSampleIntervalMSec.ToString("f3") + "MSec.");
            //    }
            //}

            Debug.Assert(session.TraceHandle != EtwNativeConstants.InvalidHandle);

            if (session.Name == KernelSessionName)
            {
                throw new NotSupportedException($"Enabling kernel flags is not supported in the NT Kernel session. Specify the flags using {nameof(StartNtKernelSession)}.");
            }

            var buffer = stackalloc byte[PropertiesSize];
            var properties = GetProperties(buffer, session.Config);


            // Initialize the stack collecting information
            const int stackTracingIdsMax = 96;      // As of 2/2015, we have a max of 56 so we are in good shape.  
            int numIDs = 0;
            var stackTracingIds = stackalloc STACK_TRACING_EVENT_ID[stackTracingIdsMax];
#if DEBUG
            // Try setting all flags, if we overflow an assert in SetStackTraceIds will fire.  
            KernelTraceEventParser.SetStackTraceIds((Keywords)(-1), stackTracingIds, stackTracingIdsMax);
#endif
            if (stackCapture != Keywords.None)
            {
                numIDs = KernelTraceEventParser.SetStackTraceIds(stackCapture, stackTracingIds, stackTracingIdsMax);
            }

            properties->LogFileMode |= EtwNativeConstants.EVENT_TRACE_SYSTEM_LOGGER_MODE;

            EtwNativeMethods.TraceSetInformation(session.TraceHandle, TRACE_INFO_CLASS.TraceStackTracingInfo, 
                stackTracingIds, (numIDs * sizeof(STACK_TRACING_EVENT_ID))).CheckWinError();

            ulong* systemTraceFlags = stackalloc ulong[1];
            systemTraceFlags[0] = (ulong)flags;
            EtwNativeMethods.TraceSetInformation(session.TraceHandle, TRACE_INFO_CLASS.TraceSystemTraceEnableFlagsInfo, 
                systemTraceFlags, sizeof(ulong)).CheckWinError();
        }

        public static void EnableProvider(EtwSession session, EtwProviderSessionConfig providerConfig, EtwEventsFilter filter = null)
        {
            Debug.Assert(session.TraceHandle != EtwNativeConstants.InvalidHandle);
            if (session.Name == KernelSessionName)
            {
                throw new NotSupportedException("Can only enable kernel events on a kernel session.");
            }

            // we require Win8.1 so filtering is always supported
            var parameters = new ENABLE_TRACE_PARAMETERS() {
                Version = EtwNativeConstants.ENABLE_TRACE_PARAMETERS_VERSION_2,
                // no filters at the moment
                FilterDescCount = 0,
                EnableFilterDesc = null
            };

            // FIXME: move it to a seperate method
            if (providerConfig.StacksEnabled)
            {
                parameters.EnableProperty |= EtwNativeConstants.EVENT_ENABLE_PROPERTY_STACK_TRACE;
            }

            var providerId = providerConfig.ProviderId;
            EtwNativeMethods.EnableTraceEx2(session.TraceHandle, ref providerId,
                EtwNativeConstants.EVENT_CONTROL_CODE_ENABLE_PROVIDER, (byte)providerConfig.ProviderLevel,
                providerConfig.MatchAnyKeywords, providerConfig.MatchAllKeywords,
                0 /* always async */, ref parameters).CheckWinError();

            // FIXME: not sure when we should set the EVENT_CONTROL_CODE_CAPTURE_STATE (in the doc, it says that
            // it still requires the provider to be enabled first
        }
    }
}
