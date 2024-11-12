using System.Windows;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace SimpleMusicApplication.services
{
    public class ImportMusicServiceBase
    {
        public async Task AddMusicToPlaylistAsync(string link, List<string> playList)
        {
            if (playList is null)
            {
                playList = new List<string>();
            }

            string videoUrl = link;

            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                MessageBox.Show("Please enter a valid YouTube link.");
                return;
            }

            try
            {
                // Initialize Youtube client
                var youtube = new YoutubeClient();

                // Get video ID and stream information
                var video = await youtube.Videos.GetAsync(videoUrl);
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

                // Get the best available audio-only stream
                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                // Set download path and file name
                string downloadPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), $"{video.Title}.mp3");



                // Download the audio stream and save it as MP3
                await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, downloadPath);

                playList.Add(downloadPath);

                MessageBox.Show($"Audio downloaded successfully to: {downloadPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }
    }
}