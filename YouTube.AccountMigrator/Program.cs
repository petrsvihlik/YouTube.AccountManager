using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Configuration;

namespace YouTube.Playground
{
    public partial class Program
    {
        static IConfigurationRoot _config;

        static Program()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddUserSecrets<Program>()
                .Build();
        }

        public static async Task Main(string[] args)
        {
            Console.WriteLine("YouTube Account Migration Tool");
            Console.WriteLine("==============================");

            try
            {
                var action = CliHelper.GetEnumFromCLI<Action>();
                Console.WriteLine("Log in with the source account to migrate the data from.");
                var sourceService = await GetService(_config.GetSection("src_account_id").Value, new[] { YouTubeService.Scope.YoutubeReadonly });
                YouTubeService? targetService = null;
                if (action == Action.Migrate)
                {
                    Console.WriteLine("Log in with the target account to migrate your data to.");
                    targetService = await GetService(_config.GetSection("target_account_id").Value, new[] { YouTubeService.Scope.Youtube });
                }

                var data = CliHelper.GetEnumFromCLI<Data>();

                switch (data)
                {
                    case Data.Playlists:
                        await Playlists(sourceService, targetService);
                        break;

                    case Data.LikedVideos:
                        await LikedVideos(sourceService, targetService, VideosResource.ListRequest.MyRatingEnum.Like);
                        break;

                    case Data.DislikedVideos:
                        await LikedVideos(sourceService, targetService, VideosResource.ListRequest.MyRatingEnum.Dislike);
                        break;
                }
            }
            catch (AggregateException ex)
            {
                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static async Task Playlists(YouTubeService source, YouTubeService? target)
        {
            var channelsListRequest = source.Channels.List("contentDetails");
            channelsListRequest.Mine = true;

            // Retrieve the contentDetails part of the channel resource for the authenticated user's channel.
            var channelsListResponse = await channelsListRequest.ExecuteAsync();

            foreach (var channel in channelsListResponse.Items)
            {
                int playlistNo = 1;
                string? nextPlayListPage = null;
                do
                {
                    var playlistsRequest = source.Playlists.List("contentDetails,snippet");
                    playlistsRequest.Mine = true;
                    playlistsRequest.MaxResults = 15;
                    playlistsRequest.PageToken = nextPlayListPage;

                    var playlists = await playlistsRequest.ExecuteAsync();


                    var likes = playlists.Items.Where(p => p.Snippet.Title.Contains("Like"));

                    foreach (var playlist in playlists.Items)
                    {
                        Console.WriteLine($"{playlistNo}) Videos in list {playlist.Snippet.Title}");
                        playlistNo++;
                        string? nextPlaylistItemPage = null;
                        do
                        {
                            var playlistItemsListRequest = source.PlaylistItems.List("snippet");
                            playlistItemsListRequest.PlaylistId = playlist.Id;
                            playlistItemsListRequest.MaxResults = 50;
                            playlistItemsListRequest.PageToken = nextPlaylistItemPage;

                            var playlistItemsListResponse = await playlistItemsListRequest.ExecuteAsync();

                            foreach (var playlistItem in playlistItemsListResponse.Items)
                            {
                                Console.WriteLine($"\t{playlistItem.Snippet.Title} ({playlistItem.Snippet.ResourceId.VideoId})");
                            }

                            nextPlaylistItemPage = playlistItemsListResponse.NextPageToken;
                        } while (nextPlaylistItemPage != null);
                    }
                    nextPlayListPage = playlists.NextPageToken;
                } while (nextPlayListPage != null);

            }
        }


        private static async Task LikedVideos(YouTubeService source, YouTubeService? target, VideosResource.ListRequest.MyRatingEnum rating)
        {
            var videosRequest = source.Videos.List("contentDetails,snippet");
            videosRequest.MyRating = rating;
            videosRequest.MaxResults = 50;

            string? nextVideoPage = null;
            int v = 1;
            do
            {
                videosRequest.PageToken = nextVideoPage;
                var videosResponse = await videosRequest.ExecuteAsync();
                foreach (var video in videosResponse.Items)
                {
                    Console.WriteLine($"{v}) {video.Snippet.Title} ({video.Id})");
                    v++;
                    try
                    {
                        if (target != null)
                        {
                            string? x = target.Videos.Rate(video.Id, (VideosResource.RateRequest.RatingEnum)rating).Execute();
                        }
                        else
                        {
                            Console.Write("\t<<Preview mode, import skipped.>>");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                nextVideoPage = videosResponse.NextPageToken;
            } while (nextVideoPage != null);

        }

        private static async Task<YouTubeService> GetService(string userId, IEnumerable<string> scopes)
        {
            UserCredential credential;


            using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
                    scopes,
                    userId,
                    CancellationToken.None,
                    new FileDataStore(typeof(Program).ToString())
                );
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = typeof(Program).ToString()
            });
            return youtubeService;
        }
    }
}
