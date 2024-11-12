using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace SimpleMusicApplication.services
{
    public class ImportMusicServiceBase
    {
        public async Task AddMusicToPlaylistAsync(string link, List<string> playList, IProgress<int> progress = null)
        {
            if (playList == null)
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

                // Sanitize the video title to make it a valid file name
                string sanitizedFileName = SanitizeFileName(video.Title);

                // Set download path and file name
                string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), $"{sanitizedFileName}.mp3");

                // Ensure the directory exists
                string directoryPath = Path.GetDirectoryName(downloadPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath); // Create the directory if it doesn't exist
                }

                // Prepare the download stream
                var stream = await youtube.Videos.Streams.GetAsync(audioStreamInfo);

                // Use a MemoryStream to buffer the data while downloading
                byte[] buffer = new byte[81920]; // 80 KB buffer

                using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                {
                    int bytesRead;
                    long totalBytesRead = 0;

                    // Read the stream and write it to the file
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;

                        // Calculate and report download progress
                        var progressPercentage = (int)((double)totalBytesRead / stream.Length * 100);
                        progress?.Report(progressPercentage);  // Report progress
                    }
                }

                // Add the downloaded file path to the playlist
                playList.Add(downloadPath);

                // Show the download path in a message box (optional)
                MessageBox.Show($"Audio downloaded successfully to: {downloadPath}");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        // Method to sanitize the file name by removing invalid characters
        private string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName.Length > 255 ? fileName.Substring(0, 255) : fileName;
        }
    }
}
