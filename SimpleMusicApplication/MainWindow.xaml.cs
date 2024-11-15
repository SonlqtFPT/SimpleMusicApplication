﻿using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;

using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;



namespace SimpleMusicApplication
{
    public partial class MainWindow : Window
    {
        private WaveOutEvent waveOutDevice;
        private WaveStream audioFileReader;



        public List<string> playlist = new List<string>();

        private List<int> playedIndices = new List<int>();
        private int currentTrackIndex = 0;
        private bool isShuffle = false;
        private DispatcherTimer positionTimer;
        private bool isDraggingSlider = false;

        private bool isAutoplay = false;


        private TaskbarIcon _trayIcon;
        private enum LoopMode
        {
            None,
            All,
            One
        }

        private LoopMode currentLoopMode = LoopMode.All;

        private TimeSpan totalListeningTime = TimeSpan.Zero;


        public MainWindow()
        {
            InitializeComponent();
            string iconPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", "music.ico");

            if (File.Exists(iconPath))
            {
                Icon icon = new Icon(iconPath);
                _trayIcon = (TaskbarIcon)this.Resources["TrayIcon"];
                _trayIcon.Icon = icon;
                _trayIcon.Visibility = Visibility.Visible;
                _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;
            }
            else
            {
                MessageBox.Show($"Icon file not found at {iconPath}");
            }

            waveOutDevice = new WaveOutEvent();
            waveOutDevice.Volume = (float)VolumeSlider.Value; // Set initial volume

            positionTimer = new DispatcherTimer();
            positionTimer.Interval = TimeSpan.FromSeconds(1);
            positionTimer.Tick += PositionTimer_Tick;

            waveOutDevice.PlaybackStopped += OnPlaybackStopped;
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }


        private async void AddMusicButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    if (!playlist.Contains(file))
                    {
                        playlist.Add(file);
                        PlaylistListBox.Items.Add(Path.GetFileName(file));
                    }
                }
                await SavePlaylistAsync("playlist.txt");
            }
        }

        private async Task SavePlaylistAsync(string filename)
        {
            if (playlist.Count > 0)
            {
                await File.WriteAllLinesAsync(filename, playlist);
                MessageBox.Show("Playlist saved successfully!", "Save Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("The playlist is empty. There is nothing to save.", "Save Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (audioFileReader == null && PlaylistListBox.SelectedIndex >= 0)
            {
                PlaySelectedTrack();
            }
            else if (waveOutDevice != null && waveOutDevice.PlaybackState == PlaybackState.Paused)
            {
                waveOutDevice.Play();
                positionTimer.Start();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            waveOutDevice?.Pause();
            positionTimer.Stop();
        }

        private void PlaySelectedTrack()
        {
            if (PlaylistListBox.SelectedIndex < 0 || playlist.Count == 0) return;

            currentTrackIndex = PlaylistListBox.SelectedIndex;
            PlayMusic();
        }

        private void PlaylistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlaySelectedTrack();
        }

        private void SequentialMode_Checked(object sender, RoutedEventArgs e)
        {
            isShuffle = false;
            playedIndices.Clear(); // Reset played tracks for fresh shuffle
        }

        private void ShuffleMode_Checked(object sender, RoutedEventArgs e)
        {
            isShuffle = true;
            playedIndices.Clear(); // Reset played tracks for fresh shuffle
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            PlayNextTrack();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            PlayPreviousTrack();
        }

        private void PlayNextTrack()
        {
            if (isShuffle)
            {
                var random = new Random();
                int nextIndex;

                if (playedIndices.Count == playlist.Count)
                    playedIndices.Clear(); // All tracks have been played

                do
                {
                    nextIndex = random.Next(playlist.Count);
                } while (playedIndices.Contains(nextIndex));

                playedIndices.Add(nextIndex);
                currentTrackIndex = nextIndex;
            }
            else
            {
                currentTrackIndex++;
                if (currentTrackIndex >= playlist.Count)
                {
                    currentTrackIndex = 0;
                }
            }
            PlayMusic();
        }

        private void PlayPreviousTrack()
        {
            if (isShuffle && playedIndices.Count > 1)
            {
                // Remove the last played track from the history to go to the previous one
                playedIndices.RemoveAt(playedIndices.Count - 1);
                currentTrackIndex = playedIndices.Last();  // Get the previous track in history
            }
            else
            {
                // In sequential mode, go to the previous track
                currentTrackIndex = (currentTrackIndex - 1 + playlist.Count) % playlist.Count;
            }

            PlaylistListBox.SelectedIndex = currentTrackIndex;
            PlayMusic();
        }

        private void PlayMusic()
        {
            if (currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
                return;

            string filePath = playlist[currentTrackIndex];
            string fileExtension = Path.GetExtension(filePath).ToLower();

            try
            {
                // Dispose previous resources
                audioFileReader?.Dispose();

                // Detach the event handler from the old waveOutDevice if it exists
                if (waveOutDevice != null)
                {
                    waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
                    waveOutDevice.Dispose();
                }

                // Load the audio file using MediaFoundationReader for broader format support
                if (fileExtension == ".mp3")
                {
                    audioFileReader = new Mp3FileReader(filePath);

                    // Convert MP3 to PCM format (WaveFormat)
                    WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(audioFileReader);
                    audioFileReader = pcmStream;
                }
                else if (fileExtension == ".wav")
                {
                    audioFileReader = new WaveFileReader(filePath);
                }
                else if (fileExtension == ".m4a")
                {
                    audioFileReader = new MediaFoundationReader(filePath);
                }
                else
                {
                    MessageBox.Show("File extension is required mp3 or wav", "Wrong file extension", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Initialize and start playback
                waveOutDevice = new WaveOutEvent();
                waveOutDevice.Init(audioFileReader);  // Ensure this uses the correct WaveFormat
                waveOutDevice.PlaybackStopped += OnPlaybackStopped;
                waveOutDevice.Play();

                // Update UI
                MusicTitleTextBlock.Text = Path.GetFileName(filePath);
                MusicInfoTextBlock.Text = $"Duration: {audioFileReader.TotalTime.ToString(@"hh\:mm\:ss")}";
                PlaylistListBox.SelectedIndex = currentTrackIndex;

                // Ensure track index is added to playedIndices in shuffle mode only
                if (isShuffle && !playedIndices.Contains(currentTrackIndex))
                {
                    playedIndices.Add(currentTrackIndex);
                }

                // Start the position timer
                positionTimer.Start();
            }
            catch (COMException ex) when ((uint)ex.ErrorCode == 0xC00D36C4)
            {
                // Error: unsupported format, skip to the next track
                MessageBox.Show($"Unsupported format for file: {filePath}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PlayNextTrack(); // Skip to the next song
            }
            catch (Exception ex)
            {
                // Log exception details for further investigation
                Console.WriteLine($"Error during playback: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }





        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                MessageBox.Show($"Playback error: {e.Exception.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                if (currentLoopMode == LoopMode.One)
                {
                    PlayMusic();
                }
                else if (currentLoopMode == LoopMode.All)
                {
                    PlayNextTrack(); // Move to the next track
                }
                else
                {
                    waveOutDevice.Stop();
                    return;
                }
            }
        }

        private async void SavePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Text Files|*.txt",
                Title = "Save Playlist"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await SavePlaylistAsync(saveFileDialog.FileName);
            }
        }

        private async void LoadPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Text Files|*.txt",
                Title = "Load Playlist",
                FileName = "playlist.txt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadPlaylistAsync(openFileDialog.FileName);
            }
        }

        private async Task LoadPlaylistAsync(string filePath)
        {
            if (File.Exists(filePath))
            {
                var loadedPlaylist = await File.ReadAllLinesAsync(filePath);
                playlist.Clear();
                PlaylistListBox.Items.Clear();

                foreach (var file in loadedPlaylist)
                {
                    if (File.Exists(file))
                    {
                        playlist.Add(file);
                        PlaylistListBox.Items.Add(Path.GetFileName(file));
                    }
                    else
                    {
                        MessageBox.Show($"File not found: {file}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                MessageBox.Show("Playlist loaded successfully!", "Load Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Playlist file not found: {filePath}", "Load Playlist", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (waveOutDevice != null)
            {
                waveOutDevice.Volume = (float)VolumeSlider.Value;
            }
        }

        private void UpdatePosition()
        {
            if (audioFileReader != null && audioFileReader.Length > 0)
            {
                var currentTime = audioFileReader.CurrentTime.TotalSeconds / audioFileReader.TotalTime.TotalSeconds;
                PositionSlider.ValueChanged -= PositionSlider_ValueChanged; // Detach event handler
                PositionSlider.Value = currentTime;
                PositionSlider.ValueChanged += PositionSlider_ValueChanged; // Reattach event handler
                CurrentTimeTextBlock.Text = TimeSpan.FromSeconds(audioFileReader.CurrentTime.TotalSeconds).ToString(@"mm\:ss");
                TotalListeningTimeTextBlock.Text = $"Total Listening Time: {totalListeningTime.ToString(@"hh\:mm\:ss")}";
            }
        }

        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            if (!isDraggingSlider && waveOutDevice.PlaybackState == PlaybackState.Playing)
            {
                totalListeningTime = totalListeningTime.Add(TimeSpan.FromSeconds(1));
                UpdatePosition();
            }
        }

        private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audioFileReader != null && audioFileReader.CanSeek)
            {
                audioFileReader.CurrentTime = TimeSpan.FromSeconds(audioFileReader.TotalTime.TotalSeconds * PositionSlider.Value);
                UpdatePosition();
            }
        }

        private void PositionSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var slider = sender as Slider;
            var thumb = FindVisualChild<Thumb>(slider);

            if (thumb != null)
            {
                System.Windows.Point position = e.GetPosition(thumb);
                if (!IsPointInsideThumb(thumb, position))
                {
                    e.Handled = true; // Prevent the click from being processed if it's outside the thumb
                }
            }
        }

        private bool IsPointInsideThumb(Thumb thumb, System.Windows.Point point)
        {
            return new Rect(0, 0, thumb.ActualWidth, thumb.ActualHeight).Contains(point);
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T tChild)
                {
                    return tChild;
                }
                else
                {
                    var result = FindVisualChild<T>(child);
                    if (result != null)
                    {
                        return result;
                    }
                }

            }
            return null;
        }

        private void Youtube_Click(object sender, RoutedEventArgs e)
        {
            YoutubeWindow youtubeWindow = new();
            youtubeWindow.PlayList = playlist;
            youtubeWindow.ShowDialog();
            this.Hide();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string musicFolderPath = Properties.Settings.Default.MusicFolderPath;

            if (Directory.Exists(musicFolderPath))
            {
                LoadSongFromFolder(musicFolderPath);
            }
            else
            {
                musicFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                LoadSongFromFolder(musicFolderPath);
            }

            if (playlist != null && playlist.Count > 0)
            {
                PlaylistListBox.ItemsSource = playlist.Select(file => Path.GetFileName(file)).ToList();
            }
        }


        private void LoadSongFromFolder(string folderPath)
        {
            try
            {
                var songFiles = Directory.GetFiles(folderPath, "*.*")
                .Where(file => file.EndsWith(".mp3") || file.EndsWith(".wav") || file.EndsWith(".m4a")).ToList();

                playlist.Clear();

                foreach (var file in songFiles)
                {
                    playlist.Add(file);
                }

                PlaylistListBox.ItemsSource = songFiles.Select(Path.GetFileName).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Have error here: " + ex.Message);
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select song folder"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string folderPath = System.IO.Path.GetDirectoryName(openFileDialog.FileName);
                Properties.Settings.Default.MusicFolderPath = folderPath;
                Properties.Settings.Default.Save();
                LoadSongFromFolder(folderPath);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            _trayIcon.Visibility = Visibility.Visible;
        }

        private void LoopButton_Click(object sender, RoutedEventArgs e)
        {
            currentLoopMode = currentLoopMode switch
            {
                LoopMode.All => LoopMode.One,
                LoopMode.One => LoopMode.None,
                LoopMode.None => LoopMode.All,
                _ => LoopMode.All,
            };

            UpdateLoopButtonUI();
        }

        private void UpdateLoopButtonUI()
        {
            switch (currentLoopMode)
            {
                case LoopMode.All:
                    LoopIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/loopAll.ico"));
                    LoopText.Text = "Loop All";
                    break;
                case LoopMode.One:
                    LoopIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/loop1.ico"));
                    LoopText.Text = "Loop One";
                    break;
                case LoopMode.None:
                    LoopIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/noLoop.ico"));
                    LoopText.Text = "No Loop";
                    break;
            }
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {

            waveOutDevice?.Stop();
            audioFileReader?.Dispose();
            audioFileReader = null;

            playlist.Clear();
            PlaylistListBox.ItemsSource = null;
            PlaylistListBox.ItemsSource = playlist;

            MusicTitleTextBlock.Text = "No music playing";
            MusicInfoTextBlock.Text = "Duration: 0:00";
            PositionSlider.Value = 0;

        }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var menu = button.ContextMenu;
            menu.IsOpen = true;
        }

        private void AddFileButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void AutoplayCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            isAutoplay = true;
        }

        private void AutoplayCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            isAutoplay = false;
        }
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchQuery = SearchTextBox.Text.ToLower();
            var filteredPlaylist = playlist.Where(file => Path.GetFileName(file).ToLower().Contains(searchQuery)).ToList();
            PlaylistListBox.ItemsSource = filteredPlaylist.Select(file => Path.GetFileName(file)).ToList();
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            PlaylistListBox.ItemsSource = playlist.Select(file => Path.GetFileName(file)).ToList();
        }


        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio Files|*.mp3;*.wav";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    if (!playlist.Contains(file))
                    {
                        playlist.Add(file);
                        PlaylistListBox.ItemsSource = playlist.Select(file => Path.GetFileName(file)).ToList();

                    }
                }
            }
        }
    }
}