using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LocalDrop.Receiver
{
    public class FileTransferItem : INotifyPropertyChanged
    {
        private long _bytesReceived;
        private TransferReceivingStatus _status;

        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime StartTime { get; set; }

        public long BytesReceived
        {
            get => _bytesReceived;
            set
            {
                _bytesReceived = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentage));
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public TransferReceivingStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public double ProgressPercentage => FileSize > 0 ? BytesReceived / (double)FileSize * 100 : 0;

        public string ProgressText => $"{FormatSize(BytesReceived)} / {FormatSize(FileSize)} " +
                                    $"({ProgressPercentage:0.0}%)";

        public string StatusMessage => Status switch
        {
            TransferReceivingStatus.Receiving => "接收中...",
            TransferReceivingStatus.Completed => $"完成于 {DateTime.Now:t}",
            TransferReceivingStatus.Failed => "传输失败",
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

    public enum TransferReceivingStatus
    {
        Pending,
        Receiving,
        Completed,
        Failed
    }
}
