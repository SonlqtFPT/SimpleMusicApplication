using SimpleMusicApplication.services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
        private async Task updatePlayList() {
            musicService = new ImportMusicServiceBase();
            await musicService.AddMusicToPlaylistAsync(txtYoutubeLink.Text, PlayList);
            MainWindow mainWindow = new MainWindow();
            mainWindow.playlist = PlayList;
            mainWindow.Show();
        }
        


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

       
    }
}
