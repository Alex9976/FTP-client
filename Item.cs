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

    public class Item
    {
        public string Name { get; set; }
        public ItemType Type { get; set; }
        public string Size { get; set; }
        public DateTime LastModificate { get; set; }
    }

}
