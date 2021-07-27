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
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        FtpClient ftp = new FtpClient();
        private int _previousIndex { get; set; } = -2;
        private bool _saveData { get; set; } = false;
        private bool _blockLoad { get; set; } = false;

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

            DirectoryList.ItemsSource = list;
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            InfoBox.Text = "";
            ProgressRing.IsActive = true;
            ToggleButton.Content = "Connecting...";
            ToggleButton.IsEnabled = false;
            ftp.Host = Host.Text;//"speedtest.tele2.net";//"test.rebex.net";"speedtest.tele2.net";
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
                    ProgressRing.IsActive = false;
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
                    ProgressRing.IsActive = false;
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
            var dialog = new FolderNameInputDlg();
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
                        ftp.DeleteFile(Path.Combine(ftp.CurrentDirectory, item.Name));
                        list.Remove(item);
                    }
                    catch (Exception ex)
                    {
                        InfoBox.Text = ex.Message;
                    }
                }
                else
                {
                    try
                    {
                        ftp.RemoveDirectory(Path.Combine(ftp.CurrentDirectory, item.Name));
                        list.Remove(item);
                    }
                    catch (Exception ex)
                    {
                        InfoBox.Text = ex.Message;
                    }
                }
            }
        }

        private void DirectoryList_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemInfo item = e.ClickedItem as ItemInfo;

            if (_previousIndex == list.IndexOf(item))
            {
                if (!ProgressRing.IsActive)
                {
                    InfoBox.Text = "";
                    if (item.Type == ItemType.Folder)
                    {
                        ProgressRing.IsActive = true;
                        OpenDirectory(item);
                    }
                    else if (item.Type == ItemType.File)
                    {
                        DownloadFile(item);
                    }
                }
                _previousIndex = -1;
            }
            _previousIndex = list.IndexOf(item);
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;
            if (toggleSwitch != null)
            {
                if (toggleSwitch.IsOn == true)
                {
                    _saveData = true;
                    localSettings.Values["saveData"] = _saveData;
                    localSettings.Values["host"] = Host.Text;
                    localSettings.Values["login"] = Login.Text;
                    localSettings.Values["password"] = Pass.Password;
                }
                else
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
                ftp.CurrentDirectory = Path.Combine(ftp.CurrentDirectory, "/", name);
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
                        ProgressRing.IsActive = false;
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

                        ProgressRing.IsActive = false;
                    });
                }
            });
            thread.Start();

        }

        private void RefreshDirectory()
        {
            ProgressRing.IsActive = true;
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
                        ProgressRing.IsActive = false;
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

                        ProgressRing.IsActive = false;
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

            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
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
                ftp.DownloadFile(ftp.CurrentDirectory, item.Name, file, item.SizeInBytes);
            }
        }

        private async void UpdateProgress()
        {
            bool flag = true;
            while (flag)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    _blockLoad = true;
                    Progress.Value = ftp.progress;
                    if (ftp.progress == 100)
                    {
                        flag = false;
                        FileInfo.Text = "Completed!";
                    }
                });
                Thread.Sleep(400);
            }
            Thread.Sleep(1000);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Progress.Value = 0;
                ftp.progress = 0;
                FileInfo.Text = "";
                _blockLoad = false;
                Progress.Visibility = Visibility.Collapsed;
            });
        }

        private void Upload_Click(object sender, RoutedEventArgs e)
        {
            UploadFile();
        }

        private async void UploadFile()
        {
            if (_blockLoad)
                return;

            var openPicker = new Windows.Storage.Pickers.FileOpenPicker();

            //openPicker.FileTypeChoices.Add("All types", new List<string>() { "." });
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;

            //savePicker.SuggestedFileName = item.Name;
            //Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                //FileInfo.Text = "Uploading: " + file.DisplayName;
                //Thread thread = new Thread(UpdateProgress);
                //thread.Start();
                ftp.UploadFile(ftp.CurrentDirectory, file);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDirectory();
        }
    }
}
