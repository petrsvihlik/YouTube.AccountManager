using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace YouTube.Playground
{
    public partial class Program
    {
        public class Endpoint
        {
            public YouTubeService Service { get; }
            
            public Channel Channel { get;  }

            public Endpoint(YouTubeService service, Channel channel)
            {
                Service = service;
                Channel = channel;
            }
        }
    }
}
