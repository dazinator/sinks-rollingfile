﻿using System;
using System.IO;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.RollingFileAlternate.Sinks.SizeRollingFileSink
{
    internal class SizeLimitedFileSink : ILogEventSink, IDisposable
    {
        private static readonly string ThisObjectName = typeof(SizeLimitedFileSink).Name;

        private readonly ITextFormatter formatter;
        private readonly SizeLimitedLogFileDescription sizeLimitedLogFileDescription;
        private readonly StreamWriter output;
        private readonly object syncRoot = new object();
        private bool disposed;
        private bool sizeLimitReached;
        private IFileSystem fileSystem;

        public SizeLimitedFileSink(
            ITextFormatter formatter,
            string logDirectory,
            SizeLimitedLogFileDescription sizeLimitedLogFileDescription,
            IFileSystem fileSystem,
            Encoding encoding = null)
        {
            this.formatter = formatter;
            this.sizeLimitedLogFileDescription = sizeLimitedLogFileDescription;
            this.fileSystem = fileSystem;
            this.output = OpenFileForWriting(logDirectory, sizeLimitedLogFileDescription, encoding ?? Encoding.UTF8);
        }

        internal SizeLimitedFileSink(
            ITextFormatter formatter,
            SizeLimitedLogFileDescription sizeLimitedLogFileDescription,
            StreamWriter writer)
        {
            this.formatter = formatter;
            this.sizeLimitedLogFileDescription = sizeLimitedLogFileDescription;
            this.output = writer;
        }

        private StreamWriter OpenFileForWriting(
            string folderPath,
            SizeLimitedLogFileDescription logFileDescription,
            Encoding encoding)
        {
            EnsureDirectoryCreated(folderPath);
            try
            {
                var fullPath = Path.Combine(folderPath, logFileDescription.FileName);
                var stream = fileSystem.OpenFileForAppend(fullPath);

                return new StreamWriter(stream, encoding ?? Encoding.UTF8);
            }
            catch (IOException ex)
            {
                // Unfortuantely the exception doesn't have a code to check so need to check the message instead
                if (!ex.Message.StartsWith("The process cannot access the file"))
                {
                    throw;
                }
            }

            return OpenFileForWriting(folderPath, logFileDescription.Next(), encoding);
        }

        private void EnsureDirectoryCreated(string path)
        {
            if (!fileSystem.DirectoryExists(path))
            {
                fileSystem.CreateDirectory(path);
            }
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null) throw new ArgumentNullException("logEvent");

            lock (this.syncRoot)
            {
                if (this.disposed)
                {
                    throw new ObjectDisposedException(ThisObjectName, "Cannot write to disposed file");
                }

                if (this.output == null)
                {
                    return; 
                }

                this.formatter.Format(logEvent, this.output);
                this.output.Flush();

                if (this.output.BaseStream.Length > this.sizeLimitedLogFileDescription.SizeLimitBytes)
                {
                    this.sizeLimitReached = true;
                }
            }
        }

        internal bool SizeLimitReached { get { return this.sizeLimitReached; } }

        internal SizeLimitedLogFileDescription LogFileDescription
        {
            get
            {
                return this.sizeLimitedLogFileDescription;
            }
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.output.Flush();
                this.output.Dispose();
                this.disposed = true;
            }
        }
    }
}