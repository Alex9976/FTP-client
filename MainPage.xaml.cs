using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x419

namespace FTP_client
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ObservableCollection<Item> contact = new ObservableCollection<Item>
        {
            new Item {Name="System Volume Information", Size="121", Type=ItemType.File, LastModificate=DateTime.Now },
            new Item {Name="a", Size="Folder", Type=ItemType.Folder, LastModificate=DateTime.Now  },
            new Item {Name="a", Size="121", Type=ItemType.File, LastModificate=DateTime.Now  },
            new Item {Name="a", Size="121", Type=ItemType.File, LastModificate=DateTime.Now  },
            new Item {Name="a", Size="121", Type=ItemType.File, LastModificate=DateTime.Now  }
        };

        public MainPage()
{
            this.InitializeComponent();


            DirectoryList.ItemsSource = contact;
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            //ProgressRing.IsActive = true;
            ToggleButton.Content = "Disconnect";
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            //ProgressRing.IsActive = false;
            ToggleButton.Content = "Connect";
        }

        private void CerateFolder_Click(object sender, RoutedEventArgs e)
        {
            contact.Add(new Item { Name = "a", Size = "121", Type = ItemType.File, LastModificate = DateTime.Now });
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = (MenuFlyoutItem)sender;
            DisplayDeleteFileDialog(item.DataContext as Item);
        }

        private async void DisplayDeleteFileDialog(Item item)
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
                contact.Remove(item);
            }
        }
    }
}
