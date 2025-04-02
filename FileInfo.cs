using System;

namespace LocalDrop
{
    public class FileInfo
    {
        public String fileName { get; set; }
        public long fileSize { get; set; }
        public FileType fileType { get; set; }
        public String info { get; set; }

        public string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }
            return $"{bytes:0.##} {sizes[order]}";
        }
    }
}
public enum FileType
{
    TEXT, IMG, FILE, AUDIO, VIDEO, DIR, QUICK_MESSAGE
}