using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
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
                var sourceService = await GetServiceAsync(_config.GetSection("src_account_id").Value, new[] { YouTubeService.Scope.YoutubeReadonly });
                YouTubeService? targetService = null;
                if (action == Action.Migrate)
                {
                    Console.WriteLine("Log in with the target account to migrate your data to.");
                    targetService = await GetServiceAsync(_config.GetSection("target_account_id").Value, new[] { YouTubeService.Scope.Youtube });
                }

                var data = CliHelper.GetEnumFromCLI<Data>();

                switch (data)
                {
                    case Data.Playlists:
                    case Data.PlayListItems:
                        await PlaylistsAsync(sourceService, targetService, data == Data.PlayListItems);
                        break;

                    case Data.LikedVideos:
                        await LikedVideosAsync(sourceService, targetService, VideosResource.ListRequest.MyRatingEnum.Like);
                        break;

                    case Data.DislikedVideos:
                        await LikedVideosAsync(sourceService, targetService, VideosResource.ListRequest.MyRatingEnum.Dislike);
                        break;

                    case Data.Subscriptions:
                        await SubscriptionsAsync(sourceService, targetService);
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

        private static async Task SubscriptionsAsync(YouTubeService source, YouTubeService? target)
        {
            string? nextSubscriptionPage = null;
            int subscriptionNo = 1;

            var targetChannel = await GetChannelAsync(target);
            do
            {
                var subscriptionsRequest = source.Subscriptions.List("id,contentDetails,snippet");
                subscriptionsRequest.Mine = true;
                subscriptionsRequest.MaxResults = 50;
                subscriptionsRequest.PageToken = nextSubscriptionPage;

                var subscriptions = await subscriptionsRequest.ExecuteAsync();

                Console.WriteLine("Subscriptions:");

                foreach (var subscription in subscriptions.Items)
                {
                    Console.WriteLine($"\t{subscriptionNo}) {subscription.Snippet.Title} ({subscription.Id})");
                    subscriptionNo++;

                    try
                    {
                        if (target != null)
                        {
                            subscription.Snippet.ChannelId = targetChannel.Id;
                            var insertResult = await target.Subscriptions.Insert(subscription, "id,contentDetails,snippet").ExecuteAsync();
                            Console.WriteLine($"Successfully inserted - {insertResult.Id}");
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

                nextSubscriptionPage = subscriptions.NextPageToken;
            } while (nextSubscriptionPage != null);
        }

        private static async Task<Channel> GetChannelAsync(YouTubeService service)
        {
            var channelsRequest = service.Channels.List("id,snippet,contentDetails");
            channelsRequest.Mine = true;
            channelsRequest.MaxResults = 1;
            var channels = await channelsRequest.ExecuteAsync();
            return channels.Items[0];
        }

        private static async Task PlaylistsAsync(YouTubeService source, YouTubeService? target, bool includeVideos)
        {
            var channel = await GetChannelAsync(source);
            int playlistNo = 1;
            string? nextPlayListPage = null;
            Console.WriteLine("Playlists:");
            do
            {
                var playlistsRequest = source.Playlists.List("contentDetails,snippet");
                playlistsRequest.ChannelId = channel.Id;
                playlistsRequest.MaxResults = 15;
                playlistsRequest.PageToken = nextPlayListPage;

                var playlists = await playlistsRequest.ExecuteAsync();


                //var likes = playlists.Items.Where(p => p.Snippet.Title.Contains("Like"));
                foreach (var playlist in playlists.Items)
                {
                    Console.Write($"\t{playlistNo}) {playlist.Snippet.Title}");

                    playlistNo++;
                    //target.Playlists.List("contentDetails,snippet");

                    //var insertPlaylist = target.Playlists.Insert(playlist, );

                    if (includeVideos)
                    {
                        Console.WriteLine($" videos:");
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
                    Console.WriteLine();
                }
                nextPlayListPage = playlists.NextPageToken;
            } while (nextPlayListPage != null);


        }


        private static async Task LikedVideosAsync(YouTubeService source, YouTubeService? target, VideosResource.ListRequest.MyRatingEnum rating)
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
                            string? x = await target.Videos.Rate(video.Id, (VideosResource.RateRequest.RatingEnum)rating).ExecuteAsync();
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

        private static async Task<YouTubeService> GetServiceAsync(string userId, IEnumerable<string> scopes)
        {
            UserCredential credential;

            using (var stream = new FileStream("client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
                    scopes,
                    userId + (new Random(DateTime.Now.Millisecond)).Next(0, 100), // randomize to trigger login every time
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
