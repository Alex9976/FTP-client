using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTP_client
{
    public enum ItemType
    {
        File,
        Folder
    }

    public enum FileListStyle
    {
        UnixStyle,
        WindowsStyle,
        Unknown
    }

    public class ItemInfo
    {
        public string Name { get; set; }
        public ItemType Type { get; set; }
        public string Size { get; set; }
        public string LastModificate { get; set; }
        public string Flags { get; set; }
        public string Owner { get; set; }
        public bool isEnabledOptions { get; set; } = true;
        public bool isNotReturnDirectory { get; set; } = true;
        public long SizeInBytes { get; set; } = 0;
    }

}
