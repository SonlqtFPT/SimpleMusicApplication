using SimpleMusicApplication.services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SimpleMusicApplication
{
    /// <summary>
    /// Interaction logic for YoutubeWindow.xaml
    /// </summary>
    public partial class YoutubeWindow : Window
    {
        public List<string> PlayList { get; set; }

        private ImportMusicServiceBase musicService;

        public YoutubeWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            updatePlayList();
        }

        private async Task updatePlayList()
        {
            // Show the ProgressBar and hide the buttons
            downloadProgressBar.Visibility = Visibility.Visible;

            // Create a Progress<int> to update the ProgressBar
            var progress = new Progress<int>(percent =>
            {
                // Update the progress bar value
                downloadProgressBar.Value = percent;
            });

            try
            {
                musicService = new ImportMusicServiceBase();

                // Add Music to Playlist with progress reporting
                await musicService.AddMusicToPlaylistAsync(txtYoutubeLink.Text, PlayList, progress);

                // After adding music, show the main window with the updated playlist
                MainWindow mainWindow = new MainWindow();
                mainWindow.playlist = PlayList;
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                // Handle any errors that may occur
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
            finally
            {
                // Hide the ProgressBar after the operation completes
                downloadProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}
