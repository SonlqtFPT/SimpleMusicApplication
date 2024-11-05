using System.Text;
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
using System.Windows.Input;
using System.Windows.Media;

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

        public MainWindow()
        {
            InitializeComponent();
            waveOutDevice = new WaveOutEvent();
            waveOutDevice.Volume = (float)VolumeSlider.Value; // Set initial volume

            positionTimer = new DispatcherTimer();
            positionTimer.Interval = TimeSpan.FromSeconds(1);
            positionTimer.Tick += PositionTimer_Tick;

            waveOutDevice.PlaybackStopped += OnPlaybackStopped;
        }

        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            if (audioFileReader != null && audioFileReader.Length > 0 && !isDraggingSlider)
            {
                PositionSlider.Value = audioFileReader.CurrentTime.TotalSeconds / audioFileReader.TotalTime.TotalSeconds;
            }
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
                currentTrackIndex = (currentTrackIndex + 1) % playlist.Count;
            }

            PlaylistListBox.SelectedIndex = currentTrackIndex;
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

        private void AutoplayCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            isAutoplay = true;
        }

        private void AutoplayCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            isAutoplay = false;
        }

        private void PlayMusic()
        {
            if (currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
                return;

            string filePath = playlist[currentTrackIndex];

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
                audioFileReader = new MediaFoundationReader(filePath);
                waveOutDevice = new WaveOutEvent();
                waveOutDevice.Init(audioFileReader);
                waveOutDevice.Play();

                // Update UI
                MusicTitleTextBlock.Text = Path.GetFileName(filePath);
                MusicInfoTextBlock.Text = $"Duration: {audioFileReader.TotalTime}";
                PlaylistListBox.SelectedIndex = currentTrackIndex;

                // Ensure track index is added to playedIndices in shuffle mode only
                if (isShuffle && !playedIndices.Contains(currentTrackIndex))
                {
                    playedIndices.Add(currentTrackIndex);
                }

                // Start the position timer
                positionTimer.Start();

                // Re-subscribe to the PlaybackStopped event if autoplay is enabled
                waveOutDevice.PlaybackStopped += OnPlaybackStopped;

            }
            catch (COMException ex) when ((uint)ex.ErrorCode == 0xC00D36C4)
            {
                // Error: unsupported format, skip to the next track
                MessageBox.Show($"Unsupported format for file: {filePath}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                PlayNextTrack(); // Skip to the next song
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                MessageBox.Show($"Playback error: {e.Exception.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (isAutoplay)
            {
                PlayNextTrack(); // Play the next track when the current one ends if autoplay is enabled
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
            }
        }

        private void PositionSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            var slider = sender as Slider;
            var thumb = FindVisualChild<Thumb>(slider);

            if (thumb != null)
            {
                Point position = e.GetPosition(thumb);
                if (!IsPointInsideThumb(thumb, position))
                {
                    e.Handled = true; // Prevent the click from being processed if it's outside the thumb
                }
            }
        }

        private bool IsPointInsideThumb(Thumb thumb, Point point)
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
            if(playlist != null)
            {
                PlaylistListBox.Items.Clear();
                foreach(string fileName in playlist)
                {       
                        
                        PlaylistListBox.Items.Add(Path.GetFileName(fileName));
                }
                
            }
        }
    }
}