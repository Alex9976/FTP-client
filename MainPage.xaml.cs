using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using static System.Net.Mime.MediaTypeNames;


namespace FTP_client
{

    public sealed partial class MainPage : Page
    {
        ObservableCollection<ItemInfo> list = new ObservableCollection<ItemInfo>();
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        FtpClient ftp = new FtpClient();
        private int _previousIndex { get; set; } = -2;
        private bool _saveData { get; set; } = false;
        private bool _blockLoad { get; set; } = false;
        private bool _isInitToggle { get; set; } = true;
        private DateTime _previousDateClick { get; set; } = DateTime.Now;
        public const long TicksPerMillisecond = 10000;

        public MainPage()
        {
            this.InitializeComponent();
            if (localSettings.Values["saveData"] != null)
                _saveData = (bool)localSettings.Values["saveData"];
            else
                localSettings.Values["saveData"] = false;

            SaveD.IsOn = _saveData;

            if (_saveData)
            {
                Host.Text = (string)localSettings.Values["host"];
                Login.Text = (string)localSettings.Values["login"];
                Pass.Password = (string)localSettings.Values["password"];
            }

            _isInitToggle = false;
            DirectoryList.ItemsSource = list;

            ftp.Notify += UpdateProgress;
            ftp.TransferComplete += TransferComplete;
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            InfoBox.Text = "";
            ProgressMainRing.IsActive = true;
            ToggleButton.Content = "Connecting...";
            ToggleButton.IsEnabled = false;
            ftp.Host = Host.Text;//"speedtest.tele2.net";
            ftp.Login = Login.Text;
            ftp.Password = Pass.Password;
            if (_saveData)
            {
                localSettings.Values["host"] = Host.Text;
                localSettings.Values["login"] = Login.Text;
                localSettings.Values["password"] = Pass.Password;
            }

            Thread thread = new Thread(Connect);
            thread.Start();
        }

        private async void Connect()
        {
            try
            {
                ItemInfo[] FileList = ftp.ListDirectory("");
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    list.Clear();
                    foreach (ItemInfo s in FileList)
                    {
                        list.Add(s);
                    }

                    PathHeader.Text = "Path:";
                    PathDir.Text = "/";
                    ProgressMainRing.IsActive = false;
                    RefreshButton.IsEnabled = true;
                    ToggleButton.Content = "Disconnect";
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    switch (ex.Message)
                    {
                        case "The remote server returned an error: (530) Not logged in.":
                            InfoBox.Text = "Incorrect login/password";
                            break;
                        case "The remote name could not be resolved":
                            InfoBox.Text = "Incorreect host address";
                            break;
                        default:
                            InfoBox.Text = ex.Message;
                            break;

                    }
                    PathHeader.Text = "";
                    ToggleButton.IsChecked = false;
                    ProgressMainRing.IsActive = false;
                });
            }
            finally
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ToggleButton.IsEnabled = true;
                });
            }
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            list.Clear();
            PathHeader.Text = "";
            PathDir.Text = "";
            RefreshButton.IsEnabled = false;
            ftp = new FtpClient();
            ToggleButton.Content = "Connect";
        }

        private async void CerateFolder_Click(object sender, RoutedEventArgs e)
        {
            InfoBox.Text = "";
            var dialog = new InputDialog();
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var folderName = dialog.Text;
                try
                {
                    ftp.CreateDirectory(ftp.CurrentDirectory, folderName);
                    RefreshDirectory();
                }
                catch (Exception ex)
                {
                    InfoBox.Text = ex.Message;
                }

            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            InfoBox.Text = "";
            MenuFlyoutItem item = (MenuFlyoutItem)sender;
            DisplayDeleteFileDialog(item.DataContext as ItemInfo);
        }

        private async void DisplayDeleteFileDialog(ItemInfo item)
        {
            ContentDialog deleteFileDialog = new ContentDialog
            {
                Title = $"Delete {item.Type.ToString().ToLower()} permanently?",
                Content = $"If you delete this {item.Type.ToString().ToLower()}, you won't be able to recover it. Do you want to delete it?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await deleteFileDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (item.Type == ItemType.File)
                {
                    try
                    {
                        ftp.Delete(ftp.CurrentDirectory + "/" + item.Name, ItemType.File);
                        list.Remove(item);
                    }
                    catch /*(Exception ex)*/
                    {
                        //InfoBox.Text = ex.Message;
                        InfoBox.Text = "No access to the file. it may have been deleted";
                    }
                }
                else
                {
                    try
                    {
                        ftp.Delete(ftp.CurrentDirectory + "/" + item.Name, ItemType.Folder);
                        list.Remove(item);
                    }
                    catch /*(Exception ex)*/
                    {
                        //InfoBox.Text = ex.Message;
                        InfoBox.Text = "The folder is not empty or has already been deleted";
                    }
                }
            }
        }

        private void DirectoryList_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemInfo item = e.ClickedItem as ItemInfo;

            if ((_previousIndex == list.IndexOf(item)) && (DateTime.Now.Ticks - _previousDateClick.Ticks <= TicksPerMillisecond * 500))
            {
                if (!ProgressMainRing.IsActive)
                {
                    InfoBox.Text = "";
                    if (item.Type == ItemType.Folder)
                    {
                        ProgressMainRing.IsActive = true;
                        OpenDirectory(item);
                    }
                    else if (item.Type == ItemType.File)
                    {
                        DownloadFile(item);
                    }
                }
                _previousIndex = -1;

                return;
            }
            _previousIndex = list.IndexOf(item);
            _previousDateClick = DateTime.Now;
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                if (toggleSwitch.IsOn && !_isInitToggle)
                {
                    _saveData = true;
                    localSettings.Values["saveData"] = _saveData;
                    localSettings.Values["host"] = Host.Text;
                    localSettings.Values["login"] = Login.Text;
                    localSettings.Values["password"] = Pass.Password;
                }
                else if (!toggleSwitch.IsOn && !_isInitToggle)
                {
                    _saveData = false;
                    localSettings.Values["saveData"] = _saveData;
                    localSettings.Values["host"] = "";
                    localSettings.Values["login"] = "";
                    localSettings.Values["password"] = "";
                }
            }
        }

        private void OpenDirectory(ItemInfo item)
        {
            string name = item.Name;
            if (name != "..")
            {
                ftp.CurrentDirectory = ftp.CurrentDirectory + Path.Combine(ftp.CurrentDirectory, "/", name);
            }
            else
            {
                ftp.CurrentDirectory = ftp.GetParentDirectory(ftp.CurrentDirectory);
            }

            Thread thread = new Thread(async () =>
            {
                try
                {
                    ItemInfo[] FileList = ftp.ListDirectory(ftp.CurrentDirectory);
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        list.Clear();
                        if (ftp.CurrentDirectory.Count(x => x == '/') > 0)
                            list.Add(new ItemInfo() { Type = ItemType.Folder, Name = "..", isNotReturnDirectory = false });
                        foreach (ItemInfo s in FileList)
                        {
                            list.Add(s);
                        }
                        PathDir.Text = ftp.CurrentDirectory == "" ? "/" : ftp.CurrentDirectory;
                        ProgressMainRing.IsActive = false;
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        switch (ex.Message)
                        {
                            case "The remote name could not be resolved":
                                InfoBox.Text = "Incorreect host address";
                                break;
                            default:
                                InfoBox.Text = ex.Message;
                                break;
                        }

                        ProgressMainRing.IsActive = false;
                    });
                }
            });
            thread.Start();

        }

        private void RefreshDirectory()
        {

            ProgressMainRing.IsActive = true;
            Thread thread = new Thread(async () =>
            {
                try
                {
                    ItemInfo[] FileList = ftp.ListDirectory(ftp.CurrentDirectory);
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        list.Clear();
                        if (ftp.CurrentDirectory.Count(x => x == '/') > 0)
                            list.Add(new ItemInfo() { Type = ItemType.Folder, Name = "..", isNotReturnDirectory = false });
                        foreach (ItemInfo s in FileList)
                        {
                            list.Add(s);
                        }
                        PathDir.Text = ftp.CurrentDirectory == "" ? "/" : ftp.CurrentDirectory;
                        ProgressMainRing.IsActive = false;
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        switch (ex.Message)
                        {
                            case "The remote name could not be resolved":
                                InfoBox.Text = "Incorreect host address";
                                break;
                            default:
                                InfoBox.Text = ex.Message;
                                break;
                        }

                        ProgressMainRing.IsActive = false;
                    });
                }
            });
            thread.Start();
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem menuFlyoutItem = (MenuFlyoutItem)sender;
            ItemInfo item = menuFlyoutItem.DataContext as ItemInfo;

            DownloadFile(item);
        }

        private async void DownloadFile(ItemInfo item)
        {
            if (_blockLoad)
                return;

            var savePicker = new FileSavePicker();
            savePicker.FileTypeChoices.Add("All types", new List<string>() { "." });
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;

            savePicker.SuggestedFileName = item.Name;
            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                FileInfo.Text = "Downloading: " + item.Name;
                Progress.Visibility = Visibility.Visible;
                Thread thread = new Thread(UpdateProgress);
                thread.Start();

                Thread downloadThread = new Thread(() => { 
                    ftp.DownloadFile(ftp.CurrentDirectory, item.Name, file, item.SizeInBytes);                  
                });
                downloadThread.Start();
            }
        }

        private async void UpdateProgress()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _blockLoad = true;
                Progress.Value = ftp.progress;
            });
        }

        private async void TransferComplete()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                FileInfo.Text = "Completed!";
            });
            Thread thread = new Thread(async () => {
                Thread.Sleep(1000);
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Progress.Value = 0;
                    ftp.progress = 0;
                    FileInfo.Text = "";
                    _blockLoad = false;
                    Progress.Visibility = Visibility.Collapsed;
                });
            });
            thread.Start(); 
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            UploadFile();
        }

        private async void UploadFile()
        {
            if (_blockLoad)
                return;

            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add("*");
            openPicker.SuggestedStartLocation = PickerLocationId.Downloads;

            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                //FileInfo.Text = "Uploading: " + file.DisplayName;
                //Thread thread = new Thread(UpdateProgress);
                //thread.Start();
                Thread thread = new Thread(async () =>
                {
                    ftp.UploadFile(ftp.CurrentDirectory, file);
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        RefreshDirectory();
                    });
                });
                thread.Start();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            InfoBox.Text = "";
            RefreshDirectory();
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            InfoBox.Text = "";
            MenuFlyoutItem menuFlyoutItem = (MenuFlyoutItem)sender;
            ItemInfo item = menuFlyoutItem.DataContext as ItemInfo;
            var dialog = new InputDialog();
            dialog.Title = "New name:";
            dialog.Text = item.Name;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var newName = dialog.Text;
                try
                {
                    ftp.Rename(ftp.CurrentDirectory + "/" + item.Name, newName);
                    RefreshDirectory();
                }
                catch (Exception ex)
                {
                    InfoBox.Text = ex.Message;
                }

            }
        }
    }
}
