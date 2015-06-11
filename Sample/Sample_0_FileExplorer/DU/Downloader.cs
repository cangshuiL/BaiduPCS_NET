﻿using System;
using System.IO;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Threading;

using BaiduPCS_NET;

namespace FileExplorer
{
    public class Downloader : ICancellable
    {
        public BaiduPCS pcs { get; protected set; }
        public PcsFileInfo from { get; set; }
        public string to { get; set; }
        public long DownloadedSize { get; protected set; }
        public bool Success { get; protected set; }
        public bool IsCancelled { get; protected set; }
        public Exception Error { get; protected set; }
        public bool Downloading { get; protected set; }
        public object State { get; set; }

        public event EventHandler<CompletedEventArgs> OnCompleted;
        public event EventHandler<ProgressEventArgs> Progress;

        public Downloader(BaiduPCS pcs, PcsFileInfo from, string to)
        {
            this.pcs = pcs;
            this.from = from;
            this.to = to;
        }

        public virtual void Download()
        {
            if (Downloading)
                throw new Exception("Can't download, since the previous download is not complete.");
            FileStream stream = null;
            DownloadedSize = 0;
            Success = false;
            IsCancelled = false;
            Error = null;
            Downloading = true;
            try
            {
                BaiduPCS pcs = this.pcs.clone();
                pcs.Write += onWrite;
                stream = new FileStream(to, FileMode.Create, FileAccess.Write);
                pcs.WriteUserData = stream;
                PcsRes rc = pcs.download(from.path, 0, 0);
                if (rc == PcsRes.PCS_OK)
                {
                    Success = true;
                    IsCancelled = false;
                }
                else if (IsCancelled)
                {
                    Success = false;
                    IsCancelled = true;
                }
                else
                {
                    Success = false;
                    IsCancelled = false;
                    if (Error == null)
                        Error = new Exception(pcs.getError());
                }
            }
            catch (Exception ex)
            {
                Success = false;
                IsCancelled = false;
                Error = ex;
            }
            if (stream != null)
                stream.Close();
            Downloading = false;
            fireOnCompleted(new CompletedEventArgs(Success, IsCancelled, Error));
        }

        public virtual void Cancel()
        {
            IsCancelled = true;
        }

        private uint onWrite(BaiduPCS sender, byte[] data, uint contentlength, object userdata)
        {
            if (IsCancelled)
                return 0;
            if(data.Length > 0)
            {
                Stream stream = (Stream)userdata;
                stream.Write(data, 0, data.Length);
            }
            DownloadedSize += data.Length;
            ProgressEventArgs args = new ProgressEventArgs(DownloadedSize, from.size);
            fireProgress(args);
            if (args.Cancel)
            {
                IsCancelled = true;
                return 0;
            }
            return (uint)data.Length;
        }

        protected virtual void fireProgress(ProgressEventArgs args)
        {
            if (Progress != null)
                Progress(this, args);
        }

        protected virtual void fireOnCompleted(CompletedEventArgs args)
        {
            if (OnCompleted != null)
                OnCompleted(this, args);
        }
    }
}
