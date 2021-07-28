using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FTP_client
{
    class DirectoryParser
    {
        private List<ItemInfo> _myListArray;
        readonly string patternUnixStyle = @"^(?<flags>[\w-]+)\s+(?<inode>\d+)\s+(?<owner>\w+)\s+(?<group>\w+)\s+" +
            @"(?<size>\d+)\s+(?<datetime>\w+\s+\d+\s+\d+|\w+\s+\d+\s+\d+:\d+)\s+(?<name>.+)$";
        readonly string patternWindowsStyle = @"^(?<datetime>\d+-\d+-\d+\s+\d+:\d+(?:AM|PM))\s+(?<sizeordir><DIR>|\d+)\s+(?<name>.+)$";
        readonly string[] sizeSuffixes = { "byte", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "EB" };

        public ItemInfo[] FullListing
        {
            get
            {
                return _myListArray.ToArray();
            }
        }

        public ItemInfo[] FileList
        {
            get
            {
                List<ItemInfo> _fileList = new List<ItemInfo>();
                foreach (ItemInfo thisstruct in _myListArray)
                {
                    if (thisstruct.Type == ItemType.File)
                    {
                        _fileList.Add(thisstruct);
                    }
                }
                return _fileList.ToArray();
            }
        }

        public ItemInfo[] DirectoryList
        {
            get
            {
                List<ItemInfo> _dirList = new List<ItemInfo>();
                foreach (ItemInfo thisstruct in _myListArray)
                {
                    if (thisstruct.Type == ItemType.Folder)
                    {
                        _dirList.Add(thisstruct);
                    }
                }
                return _dirList.ToArray();
            }
        }

        public DirectoryParser(string responseString)
        {
            _myListArray = GetList(responseString);
        }

        /// <summary>
        /// Gets a list of files and directories from the response
        /// </summary>
        /// <param name="datastring">Response data</param>
        /// <returns>List of files and directories</returns>
        private List<ItemInfo> GetList(string datastring)
        {
            List<ItemInfo> myListArray = new List<ItemInfo>();
            string[] dataRecords = datastring.Split('\n');
            FileListStyle _directoryListStyle = GuessFileListStyle(dataRecords);
            foreach (string s in dataRecords)
            {
                if (_directoryListStyle != FileListStyle.Unknown && s != "")
                {
                    ItemInfo f = new ItemInfo();
                    f.Name = "..";
                    switch (_directoryListStyle)
                    {
                        case FileListStyle.UnixStyle:
                            f = ParseFileStructFromUnixStyleRecord(s);
                            break;
                        case FileListStyle.WindowsStyle:
                            f = ParseFileStructFromWindowsStyleRecord(s);
                            break;
                    }
                    if (f.Name != "" && f.Name != "." && f.Name != "..")
                    {
                        myListArray.Add(f);
                    }
                }
            }
            return myListArray;
        }

        /// <summary>
        /// Parsing the record if the FTP server is running on Windows
        /// </summary>
        /// <param name="record">Record</param>
        /// <returns>File structure</returns>
        private ItemInfo ParseFileStructFromWindowsStyleRecord(string record)
        {
            ItemInfo fileStruct = new ItemInfo();
            string processstr = record.Trim();
            string dateStr = processstr.Substring(0, 8);
            processstr = processstr.Substring(8, processstr.Length - 8).Trim();
            string timeStr = processstr.Substring(0, 7);
            processstr = processstr.Substring(7, processstr.Length - 7).Trim();
            fileStruct.LastModificate = dateStr + " " + timeStr;

            Regex regexStyle = new Regex(patternWindowsStyle);
            Match match = regexStyle.Match(record);
            if (match.Groups["sizeordir"].Value == "<DIR>")
            {
                fileStruct.Type = ItemType.Folder;
                fileStruct.isEnabledOptions = false;
                fileStruct.Size = "";
            }
            else
            {
                fileStruct.Type = ItemType.File;
                fileStruct.SizeInBytes = long.Parse(match.Groups["sizeordir"].Value);
                fileStruct.Size = AddSizeSuffix(long.Parse(match.Groups["sizeordir"].Value));
            }
            fileStruct.Name = match.Groups["name"].Value.Trim();

            return fileStruct;
        }

        /// <summary>
        /// Identifies the operating system that the FTP server is running on
        /// </summary>
        /// <param name="recordList">Recordings</param>
        /// <returns>Record style</returns>
        public FileListStyle GuessFileListStyle(string[] recordList)
        {
            foreach (string s in recordList)
            {
                if (s.Length > 10
                    && Regex.IsMatch(s.Substring(0, 10), "(-|d)((-|r)(-|w)(-|x)){3}"))
                {
                    return FileListStyle.UnixStyle;
                }
                else if (s.Length > 8
                    && Regex.IsMatch(s.Substring(0, 8), "[0-9]{2}-[0-9]{2}-[0-9]{2}"))
                {
                    return FileListStyle.WindowsStyle;
                }
            }
            return FileListStyle.Unknown;
        }

        /// <summary>
        /// Parsing the record if the FTP server is running on Unix
        /// </summary>
        /// <param name="record">Record</param>
        /// <returns>File structure</returns>
        private ItemInfo ParseFileStructFromUnixStyleRecord(string record)
        {
            ItemInfo fileStruct = new ItemInfo();
            Regex regexStyle = new Regex(patternUnixStyle);
            Match match = regexStyle.Match(record);
            fileStruct.Flags = match.Groups["flags"].Value;
            fileStruct.Type = fileStruct.Flags[0] == 'd' ? ItemType.Folder : ItemType.File;
            fileStruct.Owner = match.Groups["owner"].Value;
            if (fileStruct.Type == ItemType.File)
{
                fileStruct.SizeInBytes = long.Parse(match.Groups["size"].Value, CultureInfo.GetCultureInfo("en-us"));
                fileStruct.Size = AddSizeSuffix(long.Parse(match.Groups["size"].Value, CultureInfo.GetCultureInfo("en-us")));
            }
            else
            {
                fileStruct.Size = "";
                fileStruct.isEnabledOptions = false;
            }
            fileStruct.LastModificate = getCreateTimeString(record);
            fileStruct.Name = match.Groups["name"].Value.Trim();

            return fileStruct;
        }

        /// <summary>
        /// Converts the number of bytes to KB, MB, etc.
        /// </summary>
        /// <param name="value">Size in bytes</param>
        /// <returns>Size with suffix</returns>
        private string AddSizeSuffix(long value)
        {
            if (value < 0) { return "-" + AddSizeSuffix(-value); }
            if (value == 0) { return string.Format("{0:n0} {1}", 0, sizeSuffixes[0]); }

            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));
            if (Math.Round(adjustedSize) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n0} {1}", adjustedSize, sizeSuffixes[mag]);
        }

        /// <summary>
        /// Get time from record
        /// </summary>
        /// <param name="record">Record</param>
        /// <returns>Time</returns>
        private string getCreateTimeString(string record)
        {
            string month = "(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)";
            string space = @"(\040)+";
            string day = "([0-9]|[1-3][0-9])";
            string year = "[1-2][0-9]{3}";
            string time = "[0-9]{1,2}:[0-9]{2}";
            Regex dateTimeRegex = new Regex(month + space + day + space + "(" + year + "|" + time + ")", RegexOptions.IgnoreCase);
            Match match = dateTimeRegex.Match(record);
            return match.Value;
        }
    }
}
