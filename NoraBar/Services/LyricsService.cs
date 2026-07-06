using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NoraBar.Services
{
    public class LyricLine
    {
        public TimeSpan StartTime { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class LrcLibResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("trackName")]
        public string TrackName { get; set; } = string.Empty;

        [JsonPropertyName("artistName")]
        public string ArtistName { get; set; } = string.Empty;

        [JsonPropertyName("albumName")]
        public string AlbumName { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("syncedLyrics")]
        public string SyncedLyrics { get; set; } = string.Empty;

        [JsonPropertyName("plainLyrics")]
        public string PlainLyrics { get; set; } = string.Empty;
    }

    public class LyricsService
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, List<LyricLine>> _cache = new();

        public LyricsService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NoraBar-App-Lyrics-Fetcher");
        }

        public async Task<List<LyricLine>?> GetLyricsAsync(string trackName, string artistName, string? albumName, double durationInSeconds)
        {
            if (string.IsNullOrWhiteSpace(trackName) || string.IsNullOrWhiteSpace(artistName))
            {
                return null;
            }

            // Create cache key
            string cacheKey = $"{trackName}_{artistName}_{albumName}_{durationInSeconds}";
            if (_cache.TryGetValue(cacheKey, out var cachedLyrics))
            {
                return cachedLyrics;
            }

            try
            {
                var queryParams = new List<string>
                {
                    $"track_name={Uri.EscapeDataString(trackName)}",
                    $"artist_name={Uri.EscapeDataString(artistName)}"
                };

                if (!string.IsNullOrWhiteSpace(albumName))
                {
                    queryParams.Add($"album_name={Uri.EscapeDataString(albumName)}");
                }

                if (durationInSeconds > 0)
                {
                    queryParams.Add($"duration={Math.Round(durationInSeconds)}");
                }

                string url = $"https://lrclib.net/api/get?{string.Join("&", queryParams)}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LrcLibResponse>();
                    if (result != null && !string.IsNullOrWhiteSpace(result.SyncedLyrics))
                    {
                        var parsedLyrics = ParseLrc(result.SyncedLyrics);
                        _cache[cacheKey] = parsedLyrics;
                        return parsedLyrics;
                    }
                }
            }
            catch (Exception)
            {
                // Return null if failed
            }

            return null;
        }

        private List<LyricLine> ParseLrc(string lrcData)
        {
            var lines = new List<LyricLine>();
            var stringLines = lrcData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in stringLines)
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d{2}):(\d{2}\.\d{2})\](.*)");
                if (match.Success)
                {
                    if (int.TryParse(match.Groups[1].Value, out int minutes) &&
                        double.TryParse(match.Groups[2].Value, out double seconds))
                    {
                        lines.Add(new LyricLine
                        {
                            StartTime = TimeSpan.FromSeconds(minutes * 60 + seconds),
                            Text = match.Groups[3].Value.Trim()
                        });
                    }
                }
            }

            return lines;
        }
    }
}
