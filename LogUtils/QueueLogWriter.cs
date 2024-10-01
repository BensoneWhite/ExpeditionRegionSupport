﻿using JollyCoop;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace LogUtils
{
    /// <summary>
    /// This writer class imitates logging functionality exhibited by JollyCoop logger mainly in that all log messages are
    /// placed in a queue and logged at the end of a Rain World frame
    /// </summary>
    public class QueueLogWriter : LogWriter
    {
        internal Queue<(LogID ID, LogElement Element)> LogCache = new Queue<(LogID ID, LogElement Element)>();

        public override void WriteFrom(LogRequest request)
        {
            if (request == null) return;

            AssignRequest(request);

            //Check that request state is still valid, this request may have been handled by another thread
            if (request.Status != RequestStatus.Pending)
            {
                WriteRequest = null;
                return;
            }

            try
            {
                WriteToBuffer();
            }
            finally
            {
                WriteRequest = null;
            }
        }

        public override void WriteToBuffer()
        {
            if (WriteRequest == null)
            {
                WriteFrom(UtilityCore.RequestHandler.CurrentRequest);
                return;
            }

            LogRequest request = WriteRequest;

            if (request.Status != RequestStatus.Pending) return;

            try
            {
                if (LogFilter.CheckFilterMatch(request.Data.ID, request.Data.Message))
                {
                    request.Reject(RejectionReason.FilterMatch);
                    return;
                }

                if (!PrepareLogFile(request.Data.ID))
                {
                    request.Reject(RejectionReason.LogUnavailable);
                    return;
                }

                if (!InternalWriteToBuffer(request.Data))
                {
                    request.Reject(RejectionReason.FailedToWrite);
                    return;
                }

                //All checks passed is a complete request
                request.Complete();
            }
            finally
            {
                UtilityCore.RequestHandler.RequestMayBeCompleteOrInvalid(request);
            }
        }

        internal bool InternalWriteToBuffer(LogEvents.LogMessageEventArgs logEventData)
        {
            OnLogMessageReceived(logEventData);

            LogCache.Enqueue((logEventData.ID, new LogElement(logEventData.Message, logEventData.Category == LogCategory.Error)));
            return true;
        }

        public override void WriteToFile()
        {
            Flush();
        }

        /// <summary>
        /// Writes all messages in the queue to file
        /// </summary>
        public void Flush()
        {
            while (LogCache.Count > 0)
            {
                var logEntry = LogCache.Dequeue();
                string writePath = logEntry.ID.Properties.CurrentFilePath;
                bool retryAttempt = false;

                try
                {
                retry:
                    using (FileStream stream = GetWriteStream(writePath, false))
                    {
                        if (stream == null)
                        {
                            if (!retryAttempt)
                            {
                                logEntry.ID.Properties.FileExists = false;
                                if (PrepareLogFile(logEntry.ID)) //Allow a single retry after creating the file once confirming session has been established
                                {
                                    retryAttempt = true;
                                    goto retry;
                                }
                            }
                            throw new IOException("Unable to create log file");
                        }

                        using (StreamWriter writer = new StreamWriter(stream))
                        {
                            bool fileChanged;
                            do
                            {
                                string message = logEntry.Element.logText;

                                //Behavior taken from JollyCoop, shouldn't be necessary if error category is already going to display
                                if (!logEntry.ID.Properties.ShowCategories.IsEnabled && logEntry.Element.shouldThrow)
                                    message = "[ERROR] " + message;

                                message = ApplyRules(logEntry.ID, message);
                                writer.WriteLine(message);

                                //Keep StreamWriter open while LogID remains unchanged
                                fileChanged = !LogCache.Any() || LogCache.Peek().ID != logEntry.ID;

                                if (!fileChanged)
                                    logEntry = LogCache.Dequeue();
                            }
                            while (!fileChanged);
                        }
                    }
                }
                catch (IOException ex)
                {
                    ExceptionInfo exceptionInfo = new ExceptionInfo(ex);

                    if (!RWInfo.CheckExceptionMatch(logEntry.ID, exceptionInfo)) //Only log unreported exceptions
                    {
                        OnLogMessageReceived(new LogEvents.LogMessageEventArgs(logEntry.ID, ex, LogCategory.Error));
                        RWInfo.ReportException(logEntry.ID, exceptionInfo);

                        logEntry = new(logEntry.ID, new LogElement(exceptionInfo.ToString(), false));
                        LogCache.Enqueue(logEntry);
                    }
                    break;
                }
            }
        }
    }
}
