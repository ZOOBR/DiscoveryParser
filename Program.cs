using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using AngleSharp;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;

namespace DiscoveryParser
{
    public class StartListItem
    {
        public string Title { get; set; }
        public string Id { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public char GroupStartChar { get; set; }
        public char GroupEndChar { get; set; }
        public bool Group { get; set; }
        public string UdpToHttpServer { get; set; }
        public string RootPath { get; set; }
    }
    public class M3URecord
    {
        public string Path { get; set; }
        public int Length = -1;
        public string ArtistName { get; set; }
        public string TrackName { get; set; }

    }

    public class M3UPlaylist
    {
        public string Hash { get; set; }
        public string Title { get; set; }
        public List<M3URecord> Records { get; set; }

        public override string ToString()
        {
            var m3U = $"#EXTM3U{ Environment.NewLine}";

            foreach (var m3URecord in Records)
            {
                if (string.IsNullOrEmpty(m3URecord.ArtistName))
                {

                }
                m3U += $"#EXTINF:{m3URecord.Length}" +
                       $"{(string.IsNullOrEmpty(m3URecord.ArtistName) && string.IsNullOrEmpty(m3URecord.TrackName) ? "" : ",")}" +
                       $"{m3URecord.ArtistName}{(string.IsNullOrEmpty(m3URecord.TrackName) ? "" : " - ")}{m3URecord.TrackName}{Environment.NewLine}" +
                       $"{m3URecord.Path}{Environment.NewLine}";
            }

            return m3U;

        }

        public M3UPlaylist()
        {
            Records = new List<M3URecord>();
        }
    }

  
    class Program
    {
        public class Parser
        {
            public string GetMd5Hash(HashAlgorithm md5Hash, string input)
            {

                // Convert the input string to a byte array and compute the hash.
                var data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

                // Create a new Stringbuilder to collect the bytes
                // and create a string.
                var sBuilder = new StringBuilder();

                // Loop through each byte of the hashed data 
                // and format each one as a hexadecimal string.
                foreach (var t in data)
                {
                    sBuilder.Append(t.ToString("x2"));
                }

                // Return the hexadecimal string.
                return sBuilder.ToString();
            }

            public string GetKey(string info, bool m3U)
            {
                var md5 = MD5.Create();

                string result = GetMd5Hash(md5, info);

                if (m3U)
                {
                    result = $"{result}.m3u";
                }

                return result;
            }

            public string ServerAdress = "192.168.1.8";
            public string UserId = "rider";
            public StartListItem StartListItem = new StartListItem() { Path = "Discovery", Title = "Discovery" };

            private async void Login(IBrowsingContext context, string Url)
            {
                await context.OpenAsync(Url);
                await context.Active.QuerySelector<IHtmlFormElement>("form").SubmitAsync(new
                {
                    email = "",
                    password = ""
                });
            }

            public M3UPlaylist ParseTorrentTvDiscovery(string path)
            {

                var channelsUrl = $"{path}/channels.php";
                var AuthUrl = $"{path}/auth.php";
                var playlistKey = GetKey(StartListItem.Path + UserId, true);
                var m3UPlaylist = new M3UPlaylist { Title = StartListItem.Title, Hash = playlistKey };

                var config = Configuration.Default.WithDefaultLoader().WithCookies();
                
                var browsingContext = BrowsingContext.New(config);
                
                Login(browsingContext, AuthUrl);
                var document = browsingContext.OpenAsync(channelsUrl).Result;
                
                var Categories = document.QuerySelectorAll("div[class='best-channels-wrapper']");
                foreach (var cat in Categories)
                {
                    var CategoryName = cat.Children[1].InnerHtml.Split('<').FirstOrDefault().Split(new string[] { "\t" }, StringSplitOptions.None).LastOrDefault();
                    var CategoryId = path + cat.Children[1].Children[0].Children[0].GetAttribute("Href");
                    foreach (var chan in cat.Children[2].Children)
                    {
                        var ChannelId = chan.Children[0].Children[0].GetAttribute("Href").Split('=').LastOrDefault();
                        var ChannelName = chan.TextContent.Replace("\n","").Replace("  ", "");
                    }
                }
                    //.Select(cat => new KeyValuePair<string, string>(cat.GetAttribute("Href"), cat.TextContent))
                    ;
                var categories = document.QuerySelectorAll("#best-channels-wrapper")
                    .Select(cat => new KeyValuePair<string, string>(cat.GetAttribute("best-channels-header"), cat.TextContent));

                foreach (var category in categories)
                {

                    var categoryPlaylistKey = GetKey(StartListItem.Path + UserId + category.Key, true);
                    var categoryPlaylist = new M3UPlaylist { Title = category.Value, Hash = categoryPlaylistKey };
                    var categoryUrl = $"{path}browse/0/{category.Key}/0/2";

                    document = BrowsingContext.New(config).OpenAsync(categoryUrl).Result;

                    var rows = document.QuerySelectorAll("#index table tr td:nth-child(2)").Skip(1);

                    foreach (var row in rows)
                    {

                        var links = row.QuerySelectorAll("a");
                        var download = $"{document.Origin}{links.FirstOrDefault()?.GetAttribute("Href")}";
                        var title = links.Skip(2).FirstOrDefault()?.InnerHtml;

                        //if (!GetRestriction(download + title))
                        //{
                        //    continue;
                        //}

                        var itemKey = GetKey(categoryPlaylistKey + download, false);

                        var m3URecord = new M3URecord
                        {
                            ArtistName = title,
                            Path = $"{ServerAdress}/get?userId={UserId}&itemId={itemKey}",

                        };

                        categoryPlaylist.Records.Add(m3URecord);
                        //AddItem(download, itemKey, MediaItemType.TorrentFile);
                    }

                    var m3URootRecord = new M3URecord
                    {
                        ArtistName = category.Value,
                        Path = $"{ServerAdress}/get?userId={UserId}&itemId={categoryPlaylistKey}"
                    };

                    m3UPlaylist.Records.Add(m3URootRecord);

                    //AddItem(categoryPlaylist, categoryPlaylistKey);
                }

                //AddItem(m3UPlaylist, playlistKey);
                return m3UPlaylist;
            }
        }
         static void Main(string[] args)
        {
            var Parser = new Parser();

            var item = Parser.ParseTorrentTvDiscovery("http://torrent-tv.ru");
        }
    }
}
