using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalDrop.Sender
{
    public enum TransferSendingStatus
    {
        Pending,
        Sending,
        Completed,
        Failed
    }
    public class SendTransferItem : INotifyPropertyChanged
    {
        private long _bytesSent;
        private TransferSendingStatus _status;

        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime StartTime { get; set; }

        public long BytesSent
        {
            get => _bytesSent;
            set
            {
                _bytesSent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public TransferSendingStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public double ProgressPercentage => FileSize > 0 ? BytesSent / (double)FileSize * 100 : 0;

        public string ProgressText => $"{FormatSize(BytesSent)} / {FormatSize(FileSize)} " +
                                    $"({ProgressPercentage:0.0}%)";

        public string StatusMessage => Status switch
        {
            TransferSendingStatus.Sending => "发送中...",
            TransferSendingStatus.Completed => $"完成于 {DateTime.Now:t}",
            TransferSendingStatus.Failed => "发送失败",
            _ => "等待开始"
        };

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
