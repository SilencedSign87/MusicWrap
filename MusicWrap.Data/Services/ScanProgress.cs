using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Services
{
    public sealed class ScanProgress
    {
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public ScanState State { get; set; } 
    }

    public enum ScanState
    {
        Fingerprinting,
        Scanning,
        Saving
    }
}
