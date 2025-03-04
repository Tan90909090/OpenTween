﻿// OpenTween - Client of Twitter
// Copyright (c) 2007-2011 kiri_feather (@kiri_feather) <kiri.feather@gmail.com>
//           (c) 2008-2011 Moz (@syo68k)
//           (c) 2008-2011 takeshik (@takeshik) <http://www.takeshik.org/>
//           (c) 2010-2011 anis774 (@anis774) <http://d.hatena.ne.jp/anis774/>
//           (c) 2010-2011 fantasticswallow (@f_swallow) <http://twitter.com/f_swallow>
//           (c) 2011      Egtra (@egtra) <http://dev.activebasic.com/egtra/>
//           (c) 2013      kim_upsilon (@kim_upsilon) <https://upsilo.net/~upsilon/>
// All rights reserved.
//
// This file is part of OpenTween.
//
// This program is free software; you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the Free
// Software Foundation; either version 3 of the License, or (at your option)
// any later version.
//
// This program is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License
// for more details.
//
// You should have received a copy of the GNU General Public License along
// with this program. If not, see <http://www.gnu.org/licenses/>, or write to
// the Free Software Foundation, Inc., 51 Franklin Street - Fifth Floor,
// Boston, MA 02110-1301, USA.

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTween.Api;
using OpenTween.Api.DataModel;
using OpenTween.Connection;
using OpenTween.Models;
using OpenTween.Setting;
using System.Globalization;

namespace OpenTween
{
    public class Twitter : IDisposable
    {
        #region Regexp from twitter-text-js

        // The code in this region code block incorporates works covered by
        // the following copyright and permission notices:
        //
        //   Copyright 2011 Twitter, Inc.
        //
        //   Licensed under the Apache License, Version 2.0 (the "License"); you
        //   may not use this work except in compliance with the License. You
        //   may obtain a copy of the License in the LICENSE file, or at:
        //
        //   http://www.apache.org/licenses/LICENSE-2.0
        //
        //   Unless required by applicable law or agreed to in writing, software
        //   distributed under the License is distributed on an "AS IS" BASIS,
        //   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
        //   implied. See the License for the specific language governing
        //   permissions and limitations under the License.

        //Hashtag用正規表現
        private const string LATIN_ACCENTS = @"\u00c0-\u00d6\u00d8-\u00f6\u00f8-\u00ff\u0100-\u024f\u0253\u0254\u0256\u0257\u0259\u025b\u0263\u0268\u026f\u0272\u0289\u028b\u02bb\u1e00-\u1eff";
        private const string NON_LATIN_HASHTAG_CHARS = @"\u0400-\u04ff\u0500-\u0527\u1100-\u11ff\u3130-\u3185\uA960-\uA97F\uAC00-\uD7AF\uD7B0-\uD7FF";
        //private const string CJ_HASHTAG_CHARACTERS = @"\u30A1-\u30FA\uFF66-\uFF9F\uFF10-\uFF19\uFF21-\uFF3A\uFF41-\uFF5A\u3041-\u3096\u3400-\u4DBF\u4E00-\u9FFF\u20000-\u2A6DF\u2A700-\u2B73F\u2B740-\u2B81F\u2F800-\u2FA1F";
        private const string CJ_HASHTAG_CHARACTERS = @"\u30A1-\u30FA\u30FC\u3005\uFF66-\uFF9F\uFF10-\uFF19\uFF21-\uFF3A\uFF41-\uFF5A\u3041-\u309A\u3400-\u4DBF\p{IsCJKUnifiedIdeographs}";
        private const string HASHTAG_BOUNDARY = @"^|$|\s|「|」|。|\.|!";
        private const string HASHTAG_ALPHA = "[a-z_" + LATIN_ACCENTS + NON_LATIN_HASHTAG_CHARS + CJ_HASHTAG_CHARACTERS + "]";
        private const string HASHTAG_ALPHANUMERIC = "[a-z0-9_" + LATIN_ACCENTS + NON_LATIN_HASHTAG_CHARS + CJ_HASHTAG_CHARACTERS + "]";
        private const string HASHTAG_TERMINATOR = "[^a-z0-9_" + LATIN_ACCENTS + NON_LATIN_HASHTAG_CHARS + CJ_HASHTAG_CHARACTERS + "]";
        public const string HASHTAG = "(" + HASHTAG_BOUNDARY + ")(#|＃)(" + HASHTAG_ALPHANUMERIC + "*" + HASHTAG_ALPHA + HASHTAG_ALPHANUMERIC + "*)(?=" + HASHTAG_TERMINATOR + "|" + HASHTAG_BOUNDARY + ")";
        //URL正規表現
        private const string url_valid_preceding_chars = @"(?:[^A-Za-z0-9@＠$#＃\ufffe\ufeff\uffff\u202a-\u202e]|^)";
        public const string url_invalid_without_protocol_preceding_chars = @"[-_./]$";
        private const string url_invalid_domain_chars = @"\!'#%&'\(\)*\+,\\\-\.\/:;<=>\?@\[\]\^_{|}~\$\u2000-\u200a\u0009-\u000d\u0020\u0085\u00a0\u1680\u180e\u2028\u2029\u202f\u205f\u3000\ufffe\ufeff\uffff\u202a-\u202e";
        private const string url_valid_domain_chars = @"[^" + url_invalid_domain_chars + "]";
        private const string url_valid_subdomain = @"(?:(?:" + url_valid_domain_chars + @"(?:[_-]|" + url_valid_domain_chars + @")*)?" + url_valid_domain_chars + @"\.)";
        private const string url_valid_domain_name = @"(?:(?:" + url_valid_domain_chars + @"(?:-|" + url_valid_domain_chars + @")*)?" + url_valid_domain_chars + @"\.)";
        private const string url_valid_GTLD = @"(?:(?:aero|asia|biz|cat|com|coop|edu|gov|info|int|jobs|mil|mobi|museum|name|net|org|pro|tel|travel|xxx)(?=[^0-9a-zA-Z]|$))";
        private const string url_valid_CCTLD = @"(?:(?:ac|ad|ae|af|ag|ai|al|am|an|ao|aq|ar|as|at|au|aw|ax|az|ba|bb|bd|be|bf|bg|bh|bi|bj|bm|bn|bo|br|bs|bt|bv|bw|by|bz|ca|cc|cd|cf|cg|ch|ci|ck|cl|cm|cn|co|cr|cs|cu|cv|cx|cy|cz|dd|de|dj|dk|dm|do|dz|ec|ee|eg|eh|er|es|et|eu|fi|fj|fk|fm|fo|fr|ga|gb|gd|ge|gf|gg|gh|gi|gl|gm|gn|gp|gq|gr|gs|gt|gu|gw|gy|hk|hm|hn|hr|ht|hu|id|ie|il|im|in|io|iq|ir|is|it|je|jm|jo|jp|ke|kg|kh|ki|km|kn|kp|kr|kw|ky|kz|la|lb|lc|li|lk|lr|ls|lt|lu|lv|ly|ma|mc|md|me|mg|mh|mk|ml|mm|mn|mo|mp|mq|mr|ms|mt|mu|mv|mw|mx|my|mz|na|nc|ne|nf|ng|ni|nl|no|np|nr|nu|nz|om|pa|pe|pf|pg|ph|pk|pl|pm|pn|pr|ps|pt|pw|py|qa|re|ro|rs|ru|rw|sa|sb|sc|sd|se|sg|sh|si|sj|sk|sl|sm|sn|so|sr|ss|st|su|sv|sy|sz|tc|td|tf|tg|th|tj|tk|tl|tm|tn|to|tp|tr|tt|tv|tw|tz|ua|ug|uk|us|uy|uz|va|vc|ve|vg|vi|vn|vu|wf|ws|ye|yt|za|zm|zw)(?=[^0-9a-zA-Z]|$))";
        private const string url_valid_punycode = @"(?:xn--[0-9a-z]+)";
        private const string url_valid_domain = @"(?<domain>" + url_valid_subdomain + "*" + url_valid_domain_name + "(?:" + url_valid_GTLD + "|" + url_valid_CCTLD + ")|" + url_valid_punycode + ")";
        public const string url_valid_ascii_domain = @"(?:(?:[a-z0-9" + LATIN_ACCENTS + @"]+)\.)+(?:" + url_valid_GTLD + "|" + url_valid_CCTLD + "|" + url_valid_punycode + ")";
        public const string url_invalid_short_domain = "^" + url_valid_domain_name + url_valid_CCTLD + "$";
        private const string url_valid_port_number = @"[0-9]+";

        private const string url_valid_general_path_chars = @"[a-z0-9!*';:=+,.$/%#\[\]\-_~|&" + LATIN_ACCENTS + "]";
        private const string url_balance_parens = @"(?:\(" + url_valid_general_path_chars + @"+\))";
        private const string url_valid_path_ending_chars = @"(?:[+\-a-z0-9=_#/" + LATIN_ACCENTS + "]|" + url_balance_parens + ")";
        private const string pth = "(?:" +
            "(?:" +
                url_valid_general_path_chars + "*" +
                "(?:" + url_balance_parens + url_valid_general_path_chars + "*)*" +
                url_valid_path_ending_chars +
                ")|(?:@" + url_valid_general_path_chars + "+/)" +
            ")";
        private const string qry = @"(?<query>\?[a-z0-9!?*'();:&=+$/%#\[\]\-_.,~|]*[a-z0-9_&=#/])?";
        public const string rgUrl = @"(?<before>" + url_valid_preceding_chars + ")" +
                                    "(?<url>(?<protocol>https?://)?" +
                                    "(?<domain>" + url_valid_domain + ")" +
                                    "(?::" + url_valid_port_number + ")?" +
                                    "(?<path>/" + pth + "*)?" +
                                    qry +
                                    ")";

        #endregion

        /// <summary>
        /// Twitter API のステータスページのURL
        /// </summary>
        public const string ServiceAvailabilityStatusUrl = "https://status.io.watchmouse.com/7617";

        /// <summary>
        /// ツイートへのパーマリンクURLを判定する正規表現
        /// </summary>
        public static readonly Regex StatusUrlRegex = new Regex(@"https?://([^.]+\.)?twitter\.com/(#!/)?(?<ScreenName>[a-zA-Z0-9_]+)/status(es)?/(?<StatusId>[0-9]+)(/photo)?", RegexOptions.IgnoreCase);

        /// <summary>
        /// attachment_url に指定可能な URL を判定する正規表現
        /// </summary>
        public static readonly Regex AttachmentUrlRegex = new Regex(@"https?://(
   twitter\.com/[0-9A-Za-z_]+/status/[0-9]+
 | mobile\.twitter\.com/[0-9A-Za-z_]+/status/[0-9]+
 | twitter\.com/messages/compose\?recipient_id=[0-9]+(&.+)?
)$", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        /// <summary>
        /// FavstarやaclogなどTwitter関連サービスのパーマリンクURLからステータスIDを抽出する正規表現
        /// </summary>
        public static readonly Regex ThirdPartyStatusUrlRegex = new Regex(@"https?://(?:[^.]+\.)?(?:
  favstar\.fm/users/[a-zA-Z0-9_]+/status/       # Favstar
| favstar\.fm/t/                                # Favstar (short)
| aclog\.koba789\.com/i/                        # aclog
| frtrt\.net/solo_status\.php\?status=          # RtRT
)(?<StatusId>[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        /// <summary>
        /// DM送信かどうかを判定する正規表現
        /// </summary>
        public static readonly Regex DMSendTextRegex = new Regex(@"^DM? +(?<id>[a-zA-Z0-9_]+) +(?<body>.*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public TwitterApi Api { get; }
        public TwitterConfiguration Configuration { get; private set; }
        public TwitterTextConfiguration TextConfiguration { get; private set; }

        public bool GetFollowersSuccess { get; private set; } = false;
        public bool GetNoRetweetSuccess { get; private set; } = false;

        delegate void GetIconImageDelegate(PostClass post);
        private readonly object LockObj = new object();
        private ISet<long> followerId = new HashSet<long>();
        private long[] noRTId = Array.Empty<long>();

        //プロパティからアクセスされる共通情報
        private List<string> _hashList = new List<string>();

        private string nextCursorDirectMessage = null;

        private long previousStatusId = -1L;

        //private FavoriteQueue favQueue;

        //private List<PostClass> _deletemessages = new List<PostClass>();

        public Twitter() : this(new TwitterApi())
        {
        }

        public Twitter(TwitterApi api)
        {
            this.Api = api;
            this.Configuration = TwitterConfiguration.DefaultConfiguration();
            this.TextConfiguration = TwitterTextConfiguration.DefaultConfiguration();
        }

        public TwitterApiAccessLevel AccessLevel
            => MyCommon.TwitterApiInfo.AccessLevel;

        protected void ResetApiStatus()
            => MyCommon.TwitterApiInfo.Reset();

        public void ClearAuthInfo()
        {
            Twitter.AccountState = MyCommon.ACCOUNT_STATE.Invalid;
            this.ResetApiStatus();
        }

        [Obsolete]
        public void VerifyCredentials()
        {
            try
            {
                this.VerifyCredentialsAsync().Wait();
            }
            catch (AggregateException ex) when (ex.InnerException is WebApiException)
            {
                throw new WebApiException(ex.InnerException.Message, ex);
            }
        }

        public async Task VerifyCredentialsAsync()
        {
            var user = await this.Api.AccountVerifyCredentials()
                .ConfigureAwait(false);

            this.UpdateUserStats(user);
        }

        public void Initialize(string token, string tokenSecret, string username, long userId)
        {
            //OAuth認証
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(tokenSecret) || string.IsNullOrEmpty(username))
            {
                Twitter.AccountState = MyCommon.ACCOUNT_STATE.Invalid;
            }
            this.ResetApiStatus();
            this.Api.Initialize(token, tokenSecret, userId, username);
            if (SettingManager.Common.UserstreamStartup) this.ReconnectUserStream();
        }

        internal static string PreProcessUrl(string orgData)
        {
            int posl1;
            var posl2 = 0;
            //var IDNConveter = new IdnMapping();
            var href = "<a href=\"";

            while (true)
            {
                if (orgData.IndexOf(href, posl2, StringComparison.Ordinal) > -1)
                {
                    var urlStr = "";
                    // IDN展開
                    posl1 = orgData.IndexOf(href, posl2, StringComparison.Ordinal);
                    posl1 += href.Length;
                    posl2 = orgData.IndexOf("\"", posl1, StringComparison.Ordinal);
                    urlStr = orgData.Substring(posl1, posl2 - posl1);

                    if (!urlStr.StartsWith("http://", StringComparison.Ordinal)
                        && !urlStr.StartsWith("https://", StringComparison.Ordinal)
                        && !urlStr.StartsWith("ftp://", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var replacedUrl = MyCommon.IDNEncode(urlStr);
                    if (replacedUrl == null) continue;
                    if (replacedUrl == urlStr) continue;

                    orgData = orgData.Replace("<a href=\"" + urlStr, "<a href=\"" + replacedUrl);
                    posl2 = 0;
                }
                else
                {
                    break;
                }
            }
            return orgData;
        }

        public async Task<PostClass> PostStatus(PostStatusParams param)
        {
            this.CheckAccountState();

            if (Twitter.DMSendTextRegex.IsMatch(param.Text))
            {
                var mediaId = param.MediaIds != null && param.MediaIds.Any() ? param.MediaIds[0] : (long?)null;

                await this.SendDirectMessage(param.Text, mediaId)
                    .ConfigureAwait(false);
                return null;
            }

            var response = await this.Api.StatusesUpdate(param.Text, param.InReplyToStatusId, param.MediaIds,
                    param.AutoPopulateReplyMetadata, param.ExcludeReplyUserIds, param.AttachmentUrl)
                .ConfigureAwait(false);

            var status = await response.LoadJsonAsync()
                .ConfigureAwait(false);

            this.UpdateUserStats(status.User);

            if (status.Id == this.previousStatusId)
                throw new WebApiException("OK:Delaying?");

            this.previousStatusId = status.Id;

            //投稿したものを返す
            var post = CreatePostsFromStatusData(status);
            if (this.ReadOwnPost) post.IsRead = true;
            return post;
        }

        public async Task<long> UploadMedia(IMediaItem item, string mediaCategory = null)
        {
            this.CheckAccountState();

            string mediaType;

            switch (item.Extension)
            {
                case ".png":
                    mediaType = "image/png";
                    break;
                case ".jpg":
                case ".jpeg":
                    mediaType = "image/jpeg";
                    break;
                case ".gif":
                    mediaType = "image/gif";
                    break;
                default:
                    mediaType = "application/octet-stream";
                    break;
            }

            var initResponse = await this.Api.MediaUploadInit(item.Size, mediaType, mediaCategory)
                .ConfigureAwait(false);

            var initMedia = await initResponse.LoadJsonAsync()
                .ConfigureAwait(false);

            var mediaId = initMedia.MediaId;

            await this.Api.MediaUploadAppend(mediaId, 0, item)
                .ConfigureAwait(false);

            var response = await this.Api.MediaUploadFinalize(mediaId)
                .ConfigureAwait(false);

            var media = await response.LoadJsonAsync()
                .ConfigureAwait(false);

            while (media.ProcessingInfo is TwitterUploadMediaResult.MediaProcessingInfo processingInfo)
            {
                switch (processingInfo.State)
                {
                    case "pending":
                        break;
                    case "in_progress":
                        break;
                    case "succeeded":
                        goto succeeded;
                    case "failed":
                        throw new WebApiException($"Err:Upload failed ({processingInfo.Error?.Name})");
                    default:
                        throw new WebApiException($"Err:Invalid state ({processingInfo.State})");
                }

                await Task.Delay(TimeSpan.FromSeconds(processingInfo.CheckAfterSecs ?? 5))
                    .ConfigureAwait(false);

                media = await this.Api.MediaUploadStatus(mediaId)
                    .ConfigureAwait(false);
            }

            succeeded:
            return media.MediaId;
        }

        public async Task SendDirectMessage(string postStr, long? mediaId = null)
        {
            this.CheckAccountState();
            this.CheckAccessLevel(TwitterApiAccessLevel.ReadWriteAndDirectMessage);

            var mc = Twitter.DMSendTextRegex.Match(postStr);

            var body = mc.Groups["body"].Value;
            var recipientName = mc.Groups["id"].Value;

            var recipient = await this.Api.UsersShow(recipientName)
                .ConfigureAwait(false);

            var response = await this.Api.DirectMessagesEventsNew(recipient.Id, body, mediaId)
                .ConfigureAwait(false);

            var messageEventSingle = await response.LoadJsonAsync()
                .ConfigureAwait(false);

            await this.CreateDirectMessagesEventFromJson(messageEventSingle, read: true)
                .ConfigureAwait(false);
        }

        public async Task<PostClass> PostRetweet(long id, bool read)
        {
            this.CheckAccountState();

            //データ部分の生成
            var post = TabInformations.GetInstance()[id];
            if (post == null)
                throw new WebApiException("Err:Target isn't found.");

            var target = post.RetweetedId ?? id;  //再RTの場合は元発言をRT

            var response = await this.Api.StatusesRetweet(target)
                .ConfigureAwait(false);

            var status = await response.LoadJsonAsync()
                .ConfigureAwait(false);

            //二重取得回避
            lock (LockObj)
            {
                if (TabInformations.GetInstance().ContainsKey(status.Id))
                    return null;
            }

            //Retweet判定
            if (status.RetweetedStatus == null)
                throw new WebApiException("Invalid Json!");

            //Retweetしたものを返す
            post = CreatePostsFromStatusData(status);

            //ユーザー情報
            post.IsMe = true;

            post.IsRead = read;
            post.IsOwl = false;
            if (this.ReadOwnPost) post.IsRead = true;
            post.IsDm = false;

            return post;
        }

        public string Username
            => this.Api.CurrentScreenName;

        public long UserId
            => this.Api.CurrentUserId;

        public static MyCommon.ACCOUNT_STATE AccountState { get; set; } = MyCommon.ACCOUNT_STATE.Valid;
        public bool RestrictFavCheck { get; set; }
        public bool ReadOwnPost { get; set; }

        public int FollowersCount { get; private set; }
        public int FriendsCount { get; private set; }
        public int StatusesCount { get; private set; }
        public string Location { get; private set; } = "";
        public string Bio { get; private set; } = "";

        /// <summary>ユーザーのフォロワー数などの情報を更新します</summary>
        private void UpdateUserStats(TwitterUser self)
        {
            this.FollowersCount = self.FollowersCount;
            this.FriendsCount = self.FriendsCount;
            this.StatusesCount = self.StatusesCount;
            this.Location = self.Location;
            this.Bio = self.Description;
        }

        /// <summary>
        /// 渡された取得件数がWORKERTYPEに応じた取得可能範囲に収まっているか検証する
        /// </summary>
        public static bool VerifyApiResultCount(MyCommon.WORKERTYPE type, int count)
            => count >= 20 && count <= GetMaxApiResultCount(type);

        /// <summary>
        /// 渡された取得件数が更新時の取得可能範囲に収まっているか検証する
        /// </summary>
        public static bool VerifyMoreApiResultCount(int count)
            => count >= 20 && count <= 200;

        /// <summary>
        /// 渡された取得件数が起動時の取得可能範囲に収まっているか検証する
        /// </summary>
        public static bool VerifyFirstApiResultCount(int count)
            => count >= 20 && count <= 200;

        /// <summary>
        /// WORKERTYPEに応じた取得可能な最大件数を取得する
        /// </summary>
        public static int GetMaxApiResultCount(MyCommon.WORKERTYPE type)
        {
            // 参照: REST APIs - 各endpointのcountパラメータ
            // https://dev.twitter.com/rest/public
            switch (type)
            {
                case MyCommon.WORKERTYPE.Timeline:
                case MyCommon.WORKERTYPE.Reply:
                case MyCommon.WORKERTYPE.UserTimeline:
                case MyCommon.WORKERTYPE.Favorites:
                case MyCommon.WORKERTYPE.List:  // 不明
                    return 200;

                case MyCommon.WORKERTYPE.PublicSearch:
                    return 100;

                default:
                    throw new InvalidOperationException("Invalid type: " + type);
            }
        }

        /// <summary>
        /// WORKERTYPEに応じた取得件数を取得する
        /// </summary>
        public static int GetApiResultCount(MyCommon.WORKERTYPE type, bool more, bool startup)
        {
            if (SettingManager.Common.UseAdditionalCount)
            {
                switch (type)
                {
                    case MyCommon.WORKERTYPE.Favorites:
                        if (SettingManager.Common.FavoritesCountApi != 0)
                            return SettingManager.Common.FavoritesCountApi;
                        break;
                    case MyCommon.WORKERTYPE.List:
                        if (SettingManager.Common.ListCountApi != 0)
                            return SettingManager.Common.ListCountApi;
                        break;
                    case MyCommon.WORKERTYPE.PublicSearch:
                        if (SettingManager.Common.SearchCountApi != 0)
                            return SettingManager.Common.SearchCountApi;
                        break;
                    case MyCommon.WORKERTYPE.UserTimeline:
                        if (SettingManager.Common.UserTimelineCountApi != 0)
                            return SettingManager.Common.UserTimelineCountApi;
                        break;
                }
                if (more && SettingManager.Common.MoreCountApi != 0)
                {
                    return Math.Min(SettingManager.Common.MoreCountApi, GetMaxApiResultCount(type));
                }
                if (startup && SettingManager.Common.FirstCountApi != 0 && type != MyCommon.WORKERTYPE.Reply)
                {
                    return Math.Min(SettingManager.Common.FirstCountApi, GetMaxApiResultCount(type));
                }
            }

            // 上記に当てはまらない場合の共通処理
            var count = SettingManager.Common.CountApi;

            if (type == MyCommon.WORKERTYPE.Reply)
                count = SettingManager.Common.CountApiReply;

            return Math.Min(count, GetMaxApiResultCount(type));
        }

        public async Task GetHomeTimelineApi(bool read, HomeTabModel tab, bool more, bool startup)
        {
            this.CheckAccountState();

            var count = GetApiResultCount(MyCommon.WORKERTYPE.Timeline, more, startup);

            TwitterStatus[] statuses;
            if (more)
            {
                statuses = await this.Api.StatusesHomeTimeline(count, maxId: tab.OldestId)
                    .ConfigureAwait(false);
            }
            else
            {
                statuses = await this.Api.StatusesHomeTimeline(count)
                    .ConfigureAwait(false);
            }

            var minimumId = this.CreatePostsFromJson(statuses, MyCommon.WORKERTYPE.Timeline, tab, read);
            if (minimumId != null)
                tab.OldestId = minimumId.Value;
        }

        public async Task GetMentionsTimelineApi(bool read, MentionsTabModel tab, bool more, bool startup)
        {
            this.CheckAccountState();

            var count = GetApiResultCount(MyCommon.WORKERTYPE.Reply, more, startup);

            TwitterStatus[] statuses;
            if (more)
            {
                statuses = await this.Api.StatusesMentionsTimeline(count, maxId: tab.OldestId)
                    .ConfigureAwait(false);
            }
            else
            {
                statuses = await this.Api.StatusesMentionsTimeline(count)
                    .ConfigureAwait(false);
            }

            var minimumId = this.CreatePostsFromJson(statuses, MyCommon.WORKERTYPE.Reply, tab, read);
            if (minimumId != null)
                tab.OldestId = minimumId.Value;
        }

        public async Task GetUserTimelineApi(bool read, string userName, UserTimelineTabModel tab, bool more)
        {
            this.CheckAccountState();

            var count = GetApiResultCount(MyCommon.WORKERTYPE.UserTimeline, more, false);

            TwitterStatus[] statuses;
            if (string.IsNullOrEmpty(userName))
            {
                var target = tab.ScreenName;
                if (string.IsNullOrEmpty(target)) return;
                userName = target;
                statuses = await this.Api.StatusesUserTimeline(userName, count)
                    .ConfigureAwait(false);
            }
            else
            {
                if (more)
                {
                    statuses = await this.Api.StatusesUserTimeline(userName, count, maxId: tab.OldestId)
                        .ConfigureAwait(false);
                }
                else
                {
                    statuses = await this.Api.StatusesUserTimeline(userName, count)
                        .ConfigureAwait(false);
                }
            }

            var minimumId = CreatePostsFromJson(statuses, MyCommon.WORKERTYPE.UserTimeline, tab, read);

            if (minimumId != null)
                tab.OldestId = minimumId.Value;
        }

        public async Task<PostClass> GetStatusApi(bool read, long id)
        {
            this.CheckAccountState();

            var status = await this.Api.StatusesShow(id)
                .ConfigureAwait(false);

            var item = CreatePostsFromStatusData(status);

            item.IsRead = read;
            if (item.IsMe && !read && this.ReadOwnPost) item.IsRead = true;

            return item;
        }

        public async Task GetStatusApi(bool read, long id, TabModel tab)
        {
            var post = await this.GetStatusApi(read, id)
                .ConfigureAwait(false);

            //非同期アイコン取得＆StatusDictionaryに追加
            if (tab != null && tab.IsInnerStorageTabType)
                tab.AddPostQueue(post);
            else
                TabInformations.GetInstance().AddPost(post);
        }

        private PostClass CreatePostsFromStatusData(TwitterStatus status)
            => this.CreatePostsFromStatusData(status, false);

        private PostClass CreatePostsFromStatusData(TwitterStatus status, bool favTweet)
        {
            var post = new PostClass();
            TwitterEntities entities;
            string sourceHtml;

            post.StatusId = status.Id;
            if (status.RetweetedStatus != null)
            {
                var retweeted = status.RetweetedStatus;

                post.CreatedAt = MyCommon.DateTimeParse(retweeted.CreatedAt);

                //Id
                post.RetweetedId = retweeted.Id;
                //本文
                post.TextFromApi = retweeted.FullText;
                entities = retweeted.MergedEntities;
                sourceHtml = retweeted.Source;
                //Reply先
                post.InReplyToStatusId = retweeted.InReplyToStatusId;
                post.InReplyToUser = retweeted.InReplyToScreenName;
                post.InReplyToUserId = status.InReplyToUserId;

                if (favTweet)
                {
                    post.IsFav = true;
                }
                else
                {
                    //幻覚fav対策
                    var tc = TabInformations.GetInstance().GetTabByType(MyCommon.TabUsageType.Favorites);
                    post.IsFav = tc.Contains(retweeted.Id);
                }

                if (retweeted.Coordinates != null)
                    post.PostGeo = new PostClass.StatusGeo(retweeted.Coordinates.Coordinates[0], retweeted.Coordinates.Coordinates[1]);

                //以下、ユーザー情報
                var user = retweeted.User;
                if (user != null)
                {
                    post.UserId = user.Id;
                    post.ScreenName = user.ScreenName;
                    post.Nickname = user.Name.Trim();
                    post.ImageUrl = user.ProfileImageUrlHttps;
                    post.IsProtect = user.Protected;
                }
                else
                {
                    post.UserId = 0L;
                    post.ScreenName = "?????";
                    post.Nickname = "Unknown User";
                }

                //Retweetした人
                if (status.User != null)
                {
                    post.RetweetedBy = status.User.ScreenName;
                    post.RetweetedByUserId = status.User.Id;
                    post.IsMe = post.RetweetedByUserId == this.UserId;
                }
                else
                {
                    post.RetweetedBy = "?????";
                    post.RetweetedByUserId = 0L;
                }
            }
            else
            {
                post.CreatedAt = MyCommon.DateTimeParse(status.CreatedAt);
                //本文
                post.TextFromApi = status.FullText;
                entities = status.MergedEntities;
                sourceHtml = status.Source;
                post.InReplyToStatusId = status.InReplyToStatusId;
                post.InReplyToUser = status.InReplyToScreenName;
                post.InReplyToUserId = status.InReplyToUserId;

                if (favTweet)
                {
                    post.IsFav = true;
                }
                else
                {
                    //幻覚fav対策
                    var tc = TabInformations.GetInstance().GetTabByType(MyCommon.TabUsageType.Favorites);
                    post.IsFav = tc.Contains(post.StatusId) && TabInformations.GetInstance()[post.StatusId].IsFav;
                }

                if (status.Coordinates != null)
                    post.PostGeo = new PostClass.StatusGeo(status.Coordinates.Coordinates[0], status.Coordinates.Coordinates[1]);

                //以下、ユーザー情報
                var user = status.User;
                if (user != null)
                {
                    post.UserId = user.Id;
                    post.ScreenName = user.ScreenName;
                    post.Nickname = user.Name.Trim();
                    post.ImageUrl = user.ProfileImageUrlHttps;
                    post.IsProtect = user.Protected;
                    post.IsMe = post.UserId == this.UserId;
                }
                else
                {
                    post.UserId = 0L;
                    post.ScreenName = "?????";
                    post.Nickname = "Unknown User";
                }
            }
            //HTMLに整形
            string textFromApi = post.TextFromApi;

            var quotedStatusLink = (status.RetweetedStatus ?? status).QuotedStatusPermalink;

            if (quotedStatusLink != null && entities.Urls.Any(x => x.ExpandedUrl == quotedStatusLink.Expanded))
                quotedStatusLink = null; // 移行期は entities.urls と quoted_status_permalink の両方に含まれる場合がある

            post.Text = CreateHtmlAnchor(textFromApi, entities, quotedStatusLink);
            post.TextFromApi = textFromApi;
            post.TextFromApi = this.ReplaceTextFromApi(post.TextFromApi, entities, quotedStatusLink);
            post.TextFromApi = WebUtility.HtmlDecode(post.TextFromApi);
            post.TextFromApi = post.TextFromApi.Replace("<3", "\u2661");
            post.AccessibleText = CreateAccessibleText(textFromApi, entities, (status.RetweetedStatus ?? status).QuotedStatus, quotedStatusLink);
            post.AccessibleText = WebUtility.HtmlDecode(post.AccessibleText);
            post.AccessibleText = post.AccessibleText.Replace("<3", "\u2661");

            this.ExtractEntities(entities, post.ReplyToList, post.Media);

            post.QuoteStatusIds = GetQuoteTweetStatusIds(entities, quotedStatusLink)
                .Where(x => x != post.StatusId && x != post.RetweetedId)
                .Distinct().ToArray();

            post.ExpandedUrls = entities.OfType<TwitterEntityUrl>()
                .Select(x => new PostClass.ExpandedUrlInfo(x.Url, x.ExpandedUrl))
                .ToArray();

            // メモリ使用量削減 (同一のテキストであれば同一の string インスタンスを参照させる)
            if (post.Text == post.TextFromApi)
                post.Text = post.TextFromApi;
            if (post.AccessibleText == post.TextFromApi)
                post.AccessibleText = post.TextFromApi;

            // 他の発言と重複しやすい (共通化できる) 文字列は string.Intern を通す
            post.ScreenName = string.Intern(post.ScreenName);
            post.Nickname = string.Intern(post.Nickname);
            post.ImageUrl = string.Intern(post.ImageUrl);
            post.RetweetedBy = post.RetweetedBy != null ? string.Intern(post.RetweetedBy) : null;

            //Source整形
            var (sourceText, sourceUri) = ParseSource(sourceHtml);
            post.Source = string.Intern(sourceText);
            post.SourceUri = sourceUri;

            post.IsReply = post.RetweetedId == null && post.ReplyToList.Any(x => x.UserId == this.UserId);
            post.IsExcludeReply = false;

            if (post.IsMe)
            {
                post.IsOwl = false;
            }
            else
            {
                if (followerId.Count > 0) post.IsOwl = !followerId.Contains(post.UserId);
            }

            post.IsDm = false;
            return post;
        }

        /// <summary>
        /// ツイートに含まれる引用ツイートのURLからステータスIDを抽出
        /// </summary>
        public static IEnumerable<long> GetQuoteTweetStatusIds(IEnumerable<TwitterEntity> entities, TwitterQuotedStatusPermalink quotedStatusLink)
        {
            var urls = entities.OfType<TwitterEntityUrl>().Select(x => x.ExpandedUrl);

            if (quotedStatusLink != null)
                urls = urls.Append(quotedStatusLink.Expanded);

            return GetQuoteTweetStatusIds(urls);
        }

        public static IEnumerable<long> GetQuoteTweetStatusIds(IEnumerable<string> urls)
        {
            foreach (var url in urls)
            {
                var match = Twitter.StatusUrlRegex.Match(url);
                if (match.Success)
                {
                    if (long.TryParse(match.Groups["StatusId"].Value, out var statusId))
                        yield return statusId;
                }
            }
        }

        private long? CreatePostsFromJson(TwitterStatus[] items, MyCommon.WORKERTYPE gType, TabModel tab, bool read)
        {
            long? minimumId = null;

            foreach (var status in items)
            {
                if (minimumId == null || minimumId.Value > status.Id)
                    minimumId = status.Id;

                //二重取得回避
                lock (LockObj)
                {
                    if (tab == null)
                    {
                        if (TabInformations.GetInstance().ContainsKey(status.Id)) continue;
                    }
                    else
                    {
                        if (tab.Contains(status.Id)) continue;
                    }
                }

                //RT禁止ユーザーによるもの
                if (gType != MyCommon.WORKERTYPE.UserTimeline &&
                    status.RetweetedStatus != null && this.noRTId.Contains(status.User.Id)) continue;

                var post = CreatePostsFromStatusData(status);

                post.IsRead = read;
                if (post.IsMe && !read && this.ReadOwnPost) post.IsRead = true;

                if (tab != null && tab.IsInnerStorageTabType)
                    tab.AddPostQueue(post);
                else
                    TabInformations.GetInstance().AddPost(post);
            }

            return minimumId;
        }

        private long? CreatePostsFromSearchJson(TwitterSearchResult items, PublicSearchTabModel tab, bool read, bool more)
        {
            long? minimumId = null;

            foreach (var status in items.Statuses)
            {
                if (minimumId == null || minimumId.Value > status.Id)
                    minimumId = status.Id;

                if (!more && status.Id > tab.SinceId) tab.SinceId = status.Id;
                //二重取得回避
                lock (LockObj)
                {
                    if (tab.Contains(status.Id)) continue;
                }

                var post = CreatePostsFromStatusData(status);

                post.IsRead = read;
                if ((post.IsMe && !read) && this.ReadOwnPost) post.IsRead = true;

                tab.AddPostQueue(post);
            }

            return minimumId;
        }

        private long? CreateFavoritePostsFromJson(TwitterStatus[] items, bool read)
        {
            var favTab = TabInformations.GetInstance().GetTabByType(MyCommon.TabUsageType.Favorites);
            long? minimumId = null;

            foreach (var status in items)
            {
                if (minimumId == null || minimumId.Value > status.Id)
                    minimumId = status.Id;

                //二重取得回避
                lock (LockObj)
                {
                    if (favTab.Contains(status.Id)) continue;
                }

                var post = CreatePostsFromStatusData(status, true);

                post.IsRead = read;

                TabInformations.GetInstance().AddPost(post);
            }

            return minimumId;
        }

        public async Task GetListStatus(bool read, ListTimelineTabModel tab, bool more, bool startup)
        {
            var count = GetApiResultCount(MyCommon.WORKERTYPE.List, more, startup);

            TwitterStatus[] statuses;
            if (more)
            {
                statuses = await this.Api.ListsStatuses(tab.ListInfo.Id, count, maxId: tab.OldestId, includeRTs: SettingManager.Common.IsListsIncludeRts)
                    .ConfigureAwait(false);
            }
            else
            {
                statuses = await this.Api.ListsStatuses(tab.ListInfo.Id, count, includeRTs: SettingManager.Common.IsListsIncludeRts)
                    .ConfigureAwait(false);
            }

            var minimumId = CreatePostsFromJson(statuses, MyCommon.WORKERTYPE.List, tab, read);

            if (minimumId != null)
                tab.OldestId = minimumId.Value;
        }

        /// <summary>
        /// startStatusId からリプライ先の発言を辿る。発言は posts 以外からは検索しない。
        /// </summary>
        /// <returns>posts の中から検索されたリプライチェインの末端</returns>
        internal static PostClass FindTopOfReplyChain(IDictionary<Int64, PostClass> posts, Int64 startStatusId)
        {
            if (!posts.ContainsKey(startStatusId))
                throw new ArgumentException("startStatusId (" + startStatusId + ") が posts の中から見つかりませんでした。", nameof(startStatusId));

            var nextPost = posts[startStatusId];
            while (nextPost.InReplyToStatusId != null)
            {
                if (!posts.ContainsKey(nextPost.InReplyToStatusId.Value))
                    break;
                nextPost = posts[nextPost.InReplyToStatusId.Value];
            }

            return nextPost;
        }

        public async Task GetRelatedResult(bool read, RelatedPostsTabModel tab)
        {
            var targetPost = tab.TargetPost;
            var relPosts = new Dictionary<Int64, PostClass>();
            if (targetPost.TextFromApi.Contains("@") && targetPost.InReplyToStatusId == null)
            {
                //検索結果対応
                var p = TabInformations.GetInstance()[targetPost.StatusId];
                if (p != null && p.InReplyToStatusId != null)
                {
                    targetPost = p;
                }
                else
                {
                    p = await this.GetStatusApi(read, targetPost.StatusId)
                        .ConfigureAwait(false);
                    targetPost = p;
                }
            }
            relPosts.Add(targetPost.StatusId, targetPost);

            Exception lastException = null;

            // in_reply_to_status_id を使用してリプライチェインを辿る
            var nextPost = FindTopOfReplyChain(relPosts, targetPost.StatusId);
            var loopCount = 1;
            while (nextPost.InReplyToStatusId != null && loopCount++ <= 20)
            {
                var inReplyToId = nextPost.InReplyToStatusId.Value;

                var inReplyToPost = TabInformations.GetInstance()[inReplyToId];
                if (inReplyToPost == null)
                {
                    try
                    {
                        inReplyToPost = await this.GetStatusApi(read, inReplyToId)
                            .ConfigureAwait(false);
                    }
                    catch (WebApiException ex)
                    {
                        lastException = ex;
                        break;
                    }
                }

                relPosts.Add(inReplyToPost.StatusId, inReplyToPost);

                nextPost = FindTopOfReplyChain(relPosts, nextPost.StatusId);
            }

            //MRTとかに対応のためツイート内にあるツイートを指すURLを取り込む
            var text = targetPost.Text;
            var ma = Twitter.StatusUrlRegex.Matches(text).Cast<Match>()
                .Concat(Twitter.ThirdPartyStatusUrlRegex.Matches(text).Cast<Match>());
            foreach (var _match in ma)
            {
                if (Int64.TryParse(_match.Groups["StatusId"].Value, out var _statusId))
                {
                    if (relPosts.ContainsKey(_statusId))
                        continue;

                    var p = TabInformations.GetInstance()[_statusId];
                    if (p == null)
                    {
                        try
                        {
                            p = await this.GetStatusApi(read, _statusId)
                                .ConfigureAwait(false);
                        }
                        catch (WebApiException ex)
                        {
                            lastException = ex;
                            break;
                        }
                    }

                    if (p != null)
                        relPosts.Add(p.StatusId, p);
                }
            }

            relPosts.Values.ToList().ForEach(p =>
            {
                if (p.IsMe && !read && this.ReadOwnPost)
                    p.IsRead = true;
                else
                    p.IsRead = read;

                tab.AddPostQueue(p);
            });

            if (lastException != null)
                throw new WebApiException(lastException.Message, lastException);
        }

        public async Task GetSearch(bool read, PublicSearchTabModel tab, bool more)
        {
            var count = GetApiResultCount(MyCommon.WORKERTYPE.PublicSearch, more, false);

            long? maxId = null;
            long? sinceId = null;
            if (more)
            {
                maxId = tab.OldestId - 1;
            }
            else
            {
                sinceId = tab.SinceId;
            }

            var searchResult = await this.Api.SearchTweets(tab.SearchWords, tab.SearchLang, count, maxId, sinceId)
                .ConfigureAwait(false);

            if (!TabInformations.GetInstance().ContainsTab(tab))
                return;

            var minimumId = this.CreatePostsFromSearchJson(searchResult, tab, read, more);

            if (minimumId != null)
                tab.OldestId = minimumId.Value;
        }

        private void CreateDirectMessagesFromJson(TwitterDirectMessage[] item, MyCommon.WORKERTYPE gType, bool read)
        {
            foreach (var message in item)
            {
                var post = new PostClass();
                try
                {
                    post.StatusId = message.Id;

                    //二重取得回避
                    lock (LockObj)
                    {
                        if (TabInformations.GetInstance().GetTabByType(MyCommon.TabUsageType.DirectMessage).Contains(post.StatusId)) continue;
                    }
                    //sender_id
                    //recipient_id
                    post.CreatedAt = MyCommon.DateTimeParse(message.CreatedAt);
                    //本文
                    var textFromApi = message.Text;
                    //HTMLに整形
                    post.Text = CreateHtmlAnchor(textFromApi, message.Entities, quotedStatusLink: null);
                    post.TextFromApi = this.ReplaceTextFromApi(textFromApi, message.Entities, quotedStatusLink: null);
                    post.TextFromApi = WebUtility.HtmlDecode(post.TextFromApi);
                    post.TextFromApi = post.TextFromApi.Replace("<3", "\u2661");
                    post.AccessibleText = CreateAccessibleText(textFromApi, message.Entities, quotedStatus: null, quotedStatusLink: null);
                    post.AccessibleText = WebUtility.HtmlDecode(post.AccessibleText);
                    post.AccessibleText = post.AccessibleText.Replace("<3", "\u2661");
                    post.IsFav = false;

                    this.ExtractEntities(message.Entities, post.ReplyToList, post.Media);

                    post.QuoteStatusIds = GetQuoteTweetStatusIds(message.Entities, quotedStatusLink: null)
                        .Distinct().ToArray();

                    post.ExpandedUrls = message.Entities.OfType<TwitterEntityUrl>()
                        .Select(x => new PostClass.ExpandedUrlInfo(x.Url, x.ExpandedUrl))
                        .ToArray();

                    //以下、ユーザー情報
                    TwitterUser user;
                    if (gType == MyCommon.WORKERTYPE.UserStream)
                    {
                        if (this.Api.CurrentUserId == message.Recipient.Id)
                        {
                            user = message.Sender;
                            post.IsMe = false;
                            post.IsOwl = true;
                        }
                        else
                        {
                            user = message.Recipient;
                            post.IsMe = true;
                            post.IsOwl = false;
                        }
                    }
                    else
                    {
                        if (gType == MyCommon.WORKERTYPE.DirectMessegeRcv)
                        {
                            user = message.Sender;
                            post.IsMe = false;
                            post.IsOwl = true;
                        }
                        else
                        {
                            user = message.Recipient;
                            post.IsMe = true;
                            post.IsOwl = false;
                        }
                    }

                    post.UserId = user.Id;
                    post.ScreenName = user.ScreenName;
                    post.Nickname = user.Name.Trim();
                    post.ImageUrl = user.ProfileImageUrlHttps;
                    post.IsProtect = user.Protected;

                    // メモリ使用量削減 (同一のテキストであれば同一の string インスタンスを参照させる)
                    if (post.Text == post.TextFromApi)
                        post.Text = post.TextFromApi;
                    if (post.AccessibleText == post.TextFromApi)
                        post.AccessibleText = post.TextFromApi;

                    // 他の発言と重複しやすい (共通化できる) 文字列は string.Intern を通す
                    post.ScreenName = string.Intern(post.ScreenName);
                    post.Nickname = string.Intern(post.Nickname);
                    post.ImageUrl = string.Intern(post.ImageUrl);
                }
                catch(Exception ex)
                {
                    MyCommon.TraceOut(ex, MethodBase.GetCurrentMethod().Name);
                    MessageBox.Show("Parse Error(CreateDirectMessagesFromJson)");
                    continue;
                }

                post.IsRead = read;
                if (post.IsMe && !read && this.ReadOwnPost) post.IsRead = true;
                post.IsReply = false;
                post.IsExcludeReply = false;
                post.IsDm = true;

                var dmTab = TabInformations.GetInstance().GetTabByType(MyCommon.TabUsageType.DirectMessage);
                dmTab.AddPostQueue(post);
            }
        }

        public async Task GetDirectMessageEvents(bool read, bool backward)
        {
            this.CheckAccountState();
            this.CheckAccessLevel(TwitterApiAccessLevel.ReadWriteAndDirectMessage);

            var count = 50;

            TwitterMessageEventList eventList;
            if (backward)
            {
                eventList = await this.Api.DirectMessagesEventsList(count, this.nextCursorDirectMessage)
                    .ConfigureAwait(false);
            }
            else
            {
                eventList = await this.Api.DirectMessagesEventsList(count)
                    .ConfigureAwait(false);
            }

            this.nextCursorDirectMessage = eventList.NextCursor;

            await this.CreateDirectMessagesEventFromJson(eventList, read)
                .ConfigureAwait(false);
        }

        private async Task CreateDirectMessagesEventFromJson(TwitterMessageEventSingle eventSingle, bool read)
        {
            var eventList = new TwitterMessageEventList
            {
                Apps = new Dictionary<string, TwitterMessageEventList.App>(),
                Events = new[] { eventSingle.Event },
            };

            await this.CreateDirectMessagesEventFromJson(eventList, read)
                .ConfigureAwait(false);
        }

        private async Task CreateDirectMessagesEventFromJson(TwitterMessageEventList eventList, bool read)
        {
            var events = eventList.Events
                .Where(x => x.Type == "message_create")
                .ToArray();

            if (events.Length == 0)
                return;

            var userIds = Enumerable.Concat(
                events.Select(x => x.MessageCreate.SenderId),
                events.Select(x => x.MessageCreate.Target.RecipientId)
            ).Distinct().ToArray();

            var users = (await this.Api.UsersLookup(userIds).ConfigureAwait(false))
                .ToDictionary(x => x.IdStr);

            var apps = eventList.Apps ?? new Dictionary<string, TwitterMessageEventList.App>();

            this.CreateDirectMessagesEventFromJson(events, users, apps, read);
        }

        private void CreateDirectMessagesEventFromJson(IEnumerable<TwitterMessageEvent> events, IReadOnlyDictionary<string, TwitterUser> users,
            IReadOnlyDictionary<string, TwitterMessageEventList.App> apps, bool read)
        {
            foreach (var eventItem in events)
            {
                var post = new PostClass();
                post.StatusId = long.Parse(eventItem.Id);

                var timestamp = long.Parse(eventItem.CreatedTimestamp);
                post.CreatedAt = DateTimeUtc.UnixEpoch + TimeSpan.FromTicks(timestamp * TimeSpan.TicksPerMillisecond);
                //本文
                var textFromApi = eventItem.MessageCreate.MessageData.Text;

                var entities = eventItem.MessageCreate.MessageData.Entities;
                var mediaEntity = eventItem.MessageCreate.MessageData.Attachment?.Media;

                if (mediaEntity != null)
                    entities.Media = new[] { mediaEntity };

                //HTMLに整形
                post.Text = CreateHtmlAnchor(textFromApi, entities, quotedStatusLink: null);
                post.TextFromApi = this.ReplaceTextFromApi(textFromApi, entities, quotedStatusLink: null);
                post.TextFromApi = WebUtility.HtmlDecode(post.TextFromApi);
                post.TextFromApi = post.TextFromApi.Replace("<3", "\u2661");
                post.AccessibleText = CreateAccessibleText(textFromApi, entities, quotedStatus: null, quotedStatusLink: null);
                post.AccessibleText = WebUtility.HtmlDecode(post.AccessibleText);
                post.AccessibleText = post.AccessibleText.Replace("<3", "\u2661");
                post.IsFav = false;

                this.ExtractEntities(entities, post.ReplyToList, post.Media);

                post.QuoteStatusIds = GetQuoteTweetStatusIds(entities, quotedStatusLink: null)
                    .Distinct().ToArray();

                post.ExpandedUrls = entities.OfType<TwitterEntityUrl>()
                    .Select(x => new PostClass.ExpandedUrlInfo(x.Url, x.ExpandedUrl))
                    .ToArray();

                //以下、ユーザー情報
                string userId;
                if (eventItem.MessageCreate.SenderId != this.Api.CurrentUserId.ToString(CultureInfo.InvariantCulture))
                {
                    userId = eventItem.MessageCreate.SenderId;
                    post.IsMe = false;
                    post.IsOwl = true;
                }
                else
                {
                    userId = eventItem.MessageCreate.Target.RecipientId;
                    post.IsMe = true;
                    post.IsOwl = false;
                }

                if (!users.TryGetValue(userId, out var user))
                    continue;

                post.UserId = user.Id;
                post.ScreenName = user.ScreenName;
                post.Nickname = user.Name.Trim();
                post.ImageUrl = user.ProfileImageUrlHttps;
                post.IsProtect = user.Protected;

                // メモリ使用量削減 (同一のテキストであれば同一の string インスタンスを参照させる)
                if (post.Text == post.TextFromApi)
                    post.Text = post.TextFromApi;
                if (post.AccessibleText == post.TextFromApi)
                    post.AccessibleText = post.TextFromApi;

                // 他の発言と重複しやすい (共通化できる) 文字列は string.Intern を通す
                post.ScreenName = string.Intern(post.ScreenName);
                post.Nickname = string.Intern(post.Nickname);
                post.ImageUrl = string.Intern(post.ImageUrl);

                var appId = eventItem.MessageCreate.SourceAppId;
                if (appId != null && apps.TryGetValue(appId, out var app))
                {
                    post.Source = string.Intern(app.Name);

                    try
                    {
                        post.SourceUri = new Uri(SourceUriBase, app.Url);
                    }
                    catch (UriFormatException) { }
                }

                post.IsRead = read;
                if (post.IsMe && !read && this.ReadOwnPost)
                    post.IsRead = true;
                post.IsReply = false;
                post.IsExcludeReply = false;
                post.IsDm = true;

                var dmTab = TabInformations.GetInstance().GetTabByType<DirectMessagesTabModel>();
                dmTab.AddPostQueue(post);
            }
        }

        public async Task GetFavoritesApi(bool read, FavoritesTabModel tab, bool backward)
        {
            this.CheckAccountState();

            var count = GetApiResultCount(MyCommon.WORKERTYPE.Favorites, backward, false);

            TwitterStatus[] statuses;
            if (backward)
            {
                statuses = await this.Api.FavoritesList(count, maxId: tab.OldestId)
                    .ConfigureAwait(false);
            }
            else
            {
                statuses = await this.Api.FavoritesList(count)
                    .ConfigureAwait(false);
            }

            var minimumId = this.CreateFavoritePostsFromJson(statuses, read);

            if (minimumId != null)
                tab.OldestId = minimumId.Value;
        }

        private string ReplaceTextFromApi(string text, TwitterEntities entities, TwitterQuotedStatusPermalink quotedStatusLink)
        {
            if (entities != null)
            {
                if (entities.Urls != null)
                {
                    foreach (var m in entities.Urls)
                    {
                        if (!string.IsNullOrEmpty(m.DisplayUrl)) text = text.Replace(m.Url, m.DisplayUrl);
                    }
                }
                if (entities.Media != null)
                {
                    foreach (var m in entities.Media)
                    {
                        if (!string.IsNullOrEmpty(m.DisplayUrl)) text = text.Replace(m.Url, m.DisplayUrl);
                    }
                }
            }

            if (quotedStatusLink != null)
                text += " " + quotedStatusLink.Display;

            return text;
        }

        internal static string CreateAccessibleText(string text, TwitterEntities entities, TwitterStatus quotedStatus, TwitterQuotedStatusPermalink quotedStatusLink)
        {
            if (entities == null)
                return text;

            if (entities.Urls != null)
            {
                foreach (var entity in entities.Urls)
                {
                    if (quotedStatus != null)
                    {
                        var matchStatusUrl = Twitter.StatusUrlRegex.Match(entity.ExpandedUrl);
                        if (matchStatusUrl.Success && matchStatusUrl.Groups["StatusId"].Value == quotedStatus.IdStr)
                        {
                            var quotedText = CreateAccessibleText(quotedStatus.FullText, quotedStatus.MergedEntities, quotedStatus: null, quotedStatusLink: null);
                            text = text.Replace(entity.Url, string.Format(Properties.Resources.QuoteStatus_AccessibleText, quotedStatus.User.ScreenName, quotedText));
                            continue;
                        }
                    }

                    if (!string.IsNullOrEmpty(entity.DisplayUrl))
                        text = text.Replace(entity.Url, entity.DisplayUrl);
                }
            }

            if (entities.Media != null)
            {
                foreach (var entity in entities.Media)
                {
                    if (!string.IsNullOrEmpty(entity.AltText))
                    {
                        text = text.Replace(entity.Url, string.Format(Properties.Resources.ImageAltText, entity.AltText));
                    }
                    else if (!string.IsNullOrEmpty(entity.DisplayUrl))
                    {
                        text = text.Replace(entity.Url, entity.DisplayUrl);
                    }
                }
            }

            if (quotedStatusLink != null)
            {
                var quoteText = CreateAccessibleText(quotedStatus.FullText, quotedStatus.MergedEntities, quotedStatus: null, quotedStatusLink: null);
                text += " " + string.Format(Properties.Resources.QuoteStatus_AccessibleText, quotedStatus.User.ScreenName, quoteText);
            }

            return text;
        }

        /// <summary>
        /// フォロワーIDを更新します
        /// </summary>
        /// <exception cref="WebApiException"/>
        public async Task RefreshFollowerIds()
        {
            if (MyCommon._endingFlag) return;

            var cursor = -1L;
            var newFollowerIds = Enumerable.Empty<long>();
            do
            {
                var ret = await this.Api.FollowersIds(cursor)
                    .ConfigureAwait(false);

                if (ret.Ids == null)
                    throw new WebApiException("ret.ids == null");

                newFollowerIds = newFollowerIds.Concat(ret.Ids);
                cursor = ret.NextCursor;
            } while (cursor != 0);

            this.followerId = newFollowerIds.ToHashSet();
            TabInformations.GetInstance().RefreshOwl(this.followerId);

            this.GetFollowersSuccess = true;
        }

        /// <summary>
        /// RT 非表示ユーザーを更新します
        /// </summary>
        /// <exception cref="WebApiException"/>
        public async Task RefreshNoRetweetIds()
        {
            if (MyCommon._endingFlag) return;

            this.noRTId = await this.Api.NoRetweetIds()
                .ConfigureAwait(false);

            this.GetNoRetweetSuccess = true;
        }

        /// <summary>
        /// t.co の文字列長などの設定情報を更新します
        /// </summary>
        /// <exception cref="WebApiException"/>
        public async Task RefreshConfiguration()
        {
            this.Configuration = await this.Api.Configuration()
                .ConfigureAwait(false);

            // TextConfiguration 相当の JSON を得る API が存在しないため、TransformedURLLength のみ help/configuration.json に合わせて更新する
            this.TextConfiguration.TransformedURLLength = this.Configuration.ShortUrlLengthHttps;
        }

        public async Task GetListsApi()
        {
            this.CheckAccountState();

            var ownedLists = await TwitterLists.GetAllItemsAsync(x =>
                this.Api.ListsOwnerships(this.Username, cursor: x, count: 1000))
                    .ConfigureAwait(false);

            var subscribedLists = await TwitterLists.GetAllItemsAsync(x =>
                this.Api.ListsSubscriptions(this.Username, cursor: x, count: 1000))
                    .ConfigureAwait(false);

            TabInformations.GetInstance().SubscribableLists = Enumerable.Concat(ownedLists, subscribedLists)
                .Select(x => new ListElement(x, this))
                .ToList();
        }

        public async Task DeleteList(long listId)
        {
            await this.Api.ListsDestroy(listId)
                .IgnoreResponse()
                .ConfigureAwait(false);

            var tabinfo = TabInformations.GetInstance();

            tabinfo.SubscribableLists = tabinfo.SubscribableLists
                .Where(x => x.Id != listId)
                .ToList();
        }

        public async Task<ListElement> EditList(long listId, string new_name, bool isPrivate, string description)
        {
            var response = await this.Api.ListsUpdate(listId, new_name, description, isPrivate)
                .ConfigureAwait(false);

            var list = await response.LoadJsonAsync()
                .ConfigureAwait(false);

            return new ListElement(list, this);
        }

        public async Task<long> GetListMembers(long listId, List<UserInfo> lists, long cursor)
        {
            this.CheckAccountState();

            var users = await this.Api.ListsMembers(listId, cursor)
                .ConfigureAwait(false);

            Array.ForEach(users.Users, u => lists.Add(new UserInfo(u)));

            return users.NextCursor;
        }

        public async Task CreateListApi(string listName, bool isPrivate, string description)
        {
            this.CheckAccountState();

            var response = await this.Api.ListsCreate(listName, description, isPrivate)
                .ConfigureAwait(false);

            var list = await response.LoadJsonAsync()
                .ConfigureAwait(false);

            TabInformations.GetInstance().SubscribableLists.Add(new ListElement(list, this));
        }

        public async Task<bool> ContainsUserAtList(long listId, string user)
        {
            this.CheckAccountState();

            try
            {
                await this.Api.ListsMembersShow(listId, user)
                    .ConfigureAwait(false);

                return true;
            }
            catch (TwitterApiException ex)
                when (ex.ErrorResponse.Errors.Any(x => x.Code == TwitterErrorCode.NotFound))
            {
                return false;
            }
        }

        private void ExtractEntities(TwitterEntities entities, List<(long UserId, string ScreenName)> AtList, List<MediaInfo> media)
        {
            if (entities != null)
            {
                if (entities.Hashtags != null)
                {
                    lock (this.LockObj)
                    {
                        this._hashList.AddRange(entities.Hashtags.Select(x => "#" + x.Text));
                    }
                }
                if (entities.UserMentions != null)
                {
                    foreach (var ent in entities.UserMentions)
                    {
                        AtList.Add((ent.Id, ent.ScreenName));
                    }
                }
                if (entities.Media != null)
                {
                    if (media != null)
                    {
                        foreach (var ent in entities.Media)
                        {
                            if (!media.Any(x => x.Url == ent.MediaUrlHttps))
                            {
                                if (ent.VideoInfo != null &&
                                    ent.Type == "animated_gif" || ent.Type == "video")
                                {
                                    //var videoUrl = ent.VideoInfo.Variants
                                    //    .Where(v => v.ContentType == "video/mp4")
                                    //    .OrderByDescending(v => v.Bitrate)
                                    //    .Select(v => v.Url).FirstOrDefault();
                                    media.Add(new MediaInfo(ent.MediaUrlHttps, ent.AltText, ent.ExpandedUrl));
                                }
                                else
                                    media.Add(new MediaInfo(ent.MediaUrlHttps, ent.AltText, videoUrl: null));
                            }
                        }
                    }
                }
            }
        }

        internal static string CreateHtmlAnchor(string text, TwitterEntities entities, TwitterQuotedStatusPermalink quotedStatusLink)
        {
            var mergedEntities = entities.Concat(TweetExtractor.ExtractEmojiEntities(text));

            // PostClass.ExpandedUrlInfo を使用して非同期に URL 展開を行うためここでは expanded_url を使用しない
            text = TweetFormatter.AutoLinkHtml(text, mergedEntities, keepTco: true);

            text = Regex.Replace(text, "(^|[^a-zA-Z0-9_/&#＃@＠>=.~])(sm|nm)([0-9]{1,10})", "$1<a href=\"https://www.nicovideo.jp/watch/$2$3\">$2$3</a>");
            text = PreProcessUrl(text); //IDN置換

            if (quotedStatusLink != null)
            {
                text += string.Format(" <a href=\"{0}\" title=\"{0}\">{1}</a>",
                    WebUtility.HtmlEncode(quotedStatusLink.Url),
                    WebUtility.HtmlEncode(quotedStatusLink.Display));
            }

            return text;
        }

        private static readonly Uri SourceUriBase = new Uri("https://twitter.com/");

        /// <summary>
        /// Twitter APIから得たHTML形式のsource文字列を分析し、source名とURLに分離します
        /// </summary>
        internal static (string SourceText, Uri SourceUri) ParseSource(string sourceHtml)
        {
            if (string.IsNullOrEmpty(sourceHtml))
                return ("", null);

            string sourceText;
            Uri sourceUri;

            // sourceHtmlの例: <a href="http://twitter.com" rel="nofollow">Twitter Web Client</a>

            var match = Regex.Match(sourceHtml, "^<a href=\"(?<uri>.+?)\".*?>(?<text>.+)</a>$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                sourceText = WebUtility.HtmlDecode(match.Groups["text"].Value);
                try
                {
                    var uriStr = WebUtility.HtmlDecode(match.Groups["uri"].Value);
                    sourceUri = new Uri(SourceUriBase, uriStr);
                }
                catch (UriFormatException)
                {
                    sourceUri = null;
                }
            }
            else
            {
                sourceText = WebUtility.HtmlDecode(sourceHtml);
                sourceUri = null;
            }

            return (sourceText, sourceUri);
        }

        public async Task<TwitterApiStatus> GetInfoApi()
        {
            if (Twitter.AccountState != MyCommon.ACCOUNT_STATE.Valid) return null;

            if (MyCommon._endingFlag) return null;

            var limits = await this.Api.ApplicationRateLimitStatus()
                .ConfigureAwait(false);

            MyCommon.TwitterApiInfo.UpdateFromJson(limits);

            return MyCommon.TwitterApiInfo;
        }

        /// <summary>
        /// ブロック中のユーザーを更新します
        /// </summary>
        /// <exception cref="WebApiException"/>
        public async Task RefreshBlockIds()
        {
            if (MyCommon._endingFlag) return;

            var cursor = -1L;
            var newBlockIds = Enumerable.Empty<long>();
            do
            {
                var ret = await this.Api.BlocksIds(cursor)
                    .ConfigureAwait(false);

                newBlockIds = newBlockIds.Concat(ret.Ids);
                cursor = ret.NextCursor;
            } while (cursor != 0);

            var blockIdsSet = newBlockIds.ToHashSet();
            blockIdsSet.Remove(this.UserId); // 元のソースにあったので一応残しておく

            TabInformations.GetInstance().BlockIds = blockIdsSet;
        }

        /// <summary>
        /// ミュート中のユーザーIDを更新します
        /// </summary>
        /// <exception cref="WebApiException"/>
        public async Task RefreshMuteUserIdsAsync()
        {
            if (MyCommon._endingFlag) return;

            var ids = await TwitterIds.GetAllItemsAsync(x => this.Api.MutesUsersIds(x))
                .ConfigureAwait(false);

            TabInformations.GetInstance().MuteUserIds = ids.ToHashSet();
        }

        public string[] GetHashList()
        {
            string[] hashArray;
            lock (LockObj)
            {
                hashArray = _hashList.ToArray();
                _hashList.Clear();
            }
            return hashArray;
        }

        public string AccessToken
            => ((TwitterApiConnection)this.Api.Connection).AccessToken;

        public string AccessTokenSecret
            => ((TwitterApiConnection)this.Api.Connection).AccessSecret;

        private void CheckAccountState()
        {
            if (Twitter.AccountState != MyCommon.ACCOUNT_STATE.Valid)
                throw new WebApiException("Auth error. Check your account");
        }

        private void CheckAccessLevel(TwitterApiAccessLevel accessLevelFlags)
        {
            if (!this.AccessLevel.HasFlag(accessLevelFlags))
                throw new WebApiException("Auth Err:try to re-authorization.");
        }

        public int GetTextLengthRemain(string postText)
        {
            var matchDm = Twitter.DMSendTextRegex.Match(postText);
            if (matchDm.Success)
                return this.GetTextLengthRemainDM(matchDm.Groups["body"].Value);

            return this.GetTextLengthRemainWeighted(postText);
        }

        private int GetTextLengthRemainDM(string postText)
        {
            var textLength = 0;

            var pos = 0;
            while (pos < postText.Length)
            {
                textLength++;

                if (char.IsSurrogatePair(postText, pos))
                    pos += 2; // サロゲートペアの場合は2文字分進める
                else
                    pos++;
            }

            var urls = TweetExtractor.ExtractUrls(postText);
            foreach (var url in urls)
            {
                var shortUrlLength = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? this.Configuration.ShortUrlLengthHttps
                    : this.Configuration.ShortUrlLength;

                textLength += shortUrlLength - url.Length;
            }

            return this.Configuration.DmTextCharacterLimit - textLength;
        }

        private int GetTextLengthRemainWeighted(string postText)
        {
            var config = this.TextConfiguration;
            var totalWeight = 0;

            int GetWeightFromCodepoint(int codepoint)
            {
                foreach (var weightRange in config.Ranges)
                {
                    if (codepoint >= weightRange.Start && codepoint <= weightRange.End)
                        return weightRange.Weight;
                }

                return config.DefaultWeight;
            }

            var urls = TweetExtractor.ExtractUrlEntities(postText).ToArray();
            var emojis = config.EmojiParsingEnabled
                ? TweetExtractor.ExtractEmojiEntities(postText).ToArray()
                : Array.Empty<TwitterEntityEmoji>();

            var codepoints = postText.ToCodepoints().ToArray();
            var index = 0;
            while (index < codepoints.Length)
            {
                var urlEntity = urls.FirstOrDefault(x => x.Indices[0] == index);
                if (urlEntity != null)
                {
                    totalWeight += config.TransformedURLLength * config.Scale;
                    index = urlEntity.Indices[1];
                    continue;
                }

                var emojiEntity = emojis.FirstOrDefault(x => x.Indices[0] == index);
                if (emojiEntity != null)
                {
                    totalWeight += GetWeightFromCodepoint(codepoints[index]);
                    index = emojiEntity.Indices[1];
                    continue;
                }

                var codepoint = codepoints[index];
                totalWeight += GetWeightFromCodepoint(codepoint);

                index++;
            }

            var remainWeight = config.MaxWeightedTweetLength * config.Scale - totalWeight;

            return remainWeight / config.Scale;
        }


#region "UserStream"
        public string TrackWord { get; set; } = "";
        public bool AllAtReply { get; set; } = false;

        public event EventHandler NewPostFromStream;
        public event EventHandler UserStreamStarted;
        public event EventHandler UserStreamStopped;
        public event EventHandler<PostDeletedEventArgs> PostDeleted;
        public event EventHandler<UserStreamEventReceivedEventArgs> UserStreamEventReceived;
        private DateTimeUtc _lastUserstreamDataReceived;
        private StreamAutoConnector userStreamConnector;

        public class FormattedEvent
        {
            public MyCommon.EVENTTYPE Eventtype { get; set; }
            public DateTimeUtc CreatedAt { get; set; }
            public string Event { get; set; }
            public string Username { get; set; }
            public string Target { get; set; }
            public Int64 Id { get; set; }
            public bool IsMe { get; set; }
        }

        public List<FormattedEvent> StoredEvent { get; } = new List<FormattedEvent>();

        private readonly IReadOnlyDictionary<string, MyCommon.EVENTTYPE> eventTable = new Dictionary<string, MyCommon.EVENTTYPE>
        {
            ["favorite"] = MyCommon.EVENTTYPE.Favorite,
            ["unfavorite"] = MyCommon.EVENTTYPE.Unfavorite,
            ["follow"] = MyCommon.EVENTTYPE.Follow,
            ["list_member_added"] = MyCommon.EVENTTYPE.ListMemberAdded,
            ["list_member_removed"] = MyCommon.EVENTTYPE.ListMemberRemoved,
            ["block"] = MyCommon.EVENTTYPE.Block,
            ["unblock"] = MyCommon.EVENTTYPE.Unblock,
            ["user_update"] = MyCommon.EVENTTYPE.UserUpdate,
            ["deleted"] = MyCommon.EVENTTYPE.Deleted,
            ["list_created"] = MyCommon.EVENTTYPE.ListCreated,
            ["list_destroyed"] = MyCommon.EVENTTYPE.ListDestroyed,
            ["list_updated"] = MyCommon.EVENTTYPE.ListUpdated,
            ["unfollow"] = MyCommon.EVENTTYPE.Unfollow,
            ["list_user_subscribed"] = MyCommon.EVENTTYPE.ListUserSubscribed,
            ["list_user_unsubscribed"] = MyCommon.EVENTTYPE.ListUserUnsubscribed,
            ["mute"] = MyCommon.EVENTTYPE.Mute,
            ["unmute"] = MyCommon.EVENTTYPE.Unmute,
            ["quoted_tweet"] = MyCommon.EVENTTYPE.QuotedTweet,
        };

        public bool IsUserstreamDataReceived
            => (DateTimeUtc.Now - this._lastUserstreamDataReceived).TotalSeconds < 31;

        private void userStream_MessageReceived(ITwitterStreamMessage message)
        {
            this._lastUserstreamDataReceived = DateTimeUtc.Now;

            switch (message)
            {
                case StreamMessageStatus statusMessage:
                    var status = statusMessage.Status.Normalize();

                    if (status.RetweetedStatus is TwitterStatus retweetedStatus)
                    {
                        var sourceUserId = statusMessage.Status.User.Id;
                        var targetUserId = retweetedStatus.User.Id;

                        // 自分に関係しないリツイートの場合は無視する
                        var selfUserId = this.UserId;
                        if (sourceUserId == selfUserId || targetUserId == selfUserId)
                        {
                            // 公式 RT をイベントとしても扱う
                            var evt = this.CreateEventFromRetweet(status);
                            this.StoredEvent.Insert(0, evt);
                            this.UserStreamEventReceived?.Invoke(this, new UserStreamEventReceivedEventArgs(evt));
                        }
                        // 従来通り公式 RT の表示も行うため break しない
                    }

                    this.CreatePostsFromJson(new[] { status }, MyCommon.WORKERTYPE.UserStream, null, false);
                    this.NewPostFromStream?.Invoke(this, EventArgs.Empty);
                    break;

                case StreamMessageDirectMessage dmMessage:
                    this.CreateDirectMessagesFromJson(new[] { dmMessage.DirectMessage }, MyCommon.WORKERTYPE.UserStream, false);
                    this.NewPostFromStream?.Invoke(this, EventArgs.Empty);
                    break;

                case StreamMessageDelete deleteMessage:
                    var deletedId = deleteMessage.Status?.Id ?? deleteMessage.DirectMessage?.Id;
                    if (deletedId == null)
                        break;

                    this.PostDeleted?.Invoke(this, new PostDeletedEventArgs(deletedId.Value));

                    foreach (var index in MyCommon.CountDown(this.StoredEvent.Count - 1, 0))
                    {
                        var evt = this.StoredEvent[index];
                        if (evt.Id == deletedId.Value && (evt.Event == "favorite" || evt.Event == "unfavorite"))
                        {
                            this.StoredEvent.RemoveAt(index);
                        }
                    }
                    break;

                case StreamMessageEvent eventMessage:
                    this.CreateEventFromJson(eventMessage);
                    break;

                case StreamMessageScrubGeo scrubGeoMessage:
                    TabInformations.GetInstance().ScrubGeoReserve(scrubGeoMessage.UserId, scrubGeoMessage.UpToStatusId);
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// UserStreamsから受信した公式RTをイベントに変換します
        /// </summary>
        private FormattedEvent CreateEventFromRetweet(TwitterStatus status)
        {
            return new FormattedEvent
            {
                Eventtype = MyCommon.EVENTTYPE.Retweet,
                Event = "retweet",
                CreatedAt = MyCommon.DateTimeParse(status.CreatedAt),
                IsMe = status.User.Id == this.UserId,
                Username = status.User.ScreenName,
                Target = string.Format("@{0}:{1}", new[]
                {
                    status.RetweetedStatus.User.ScreenName,
                    WebUtility.HtmlDecode(status.RetweetedStatus.FullText),
                }),
                Id = status.RetweetedStatus.Id,
            };
        }

        private void CreateEventFromJson(StreamMessageEvent message)
        {
            var eventData = message.Event;

            var evt = new FormattedEvent
            {
                CreatedAt = MyCommon.DateTimeParse(eventData.CreatedAt),
                Event = eventData.Event,
                Username = eventData.Source.ScreenName,
                IsMe = eventData.Source.Id == this.UserId,
                Eventtype = eventTable.TryGetValue(eventData.Event, out var eventType) ? eventType : MyCommon.EVENTTYPE.None,
            };

            TwitterStreamEvent<TwitterStatusCompat> tweetEvent;
            TwitterStatus tweet;

            switch (eventData.Event)
            {
                case "access_revoked":
                case "access_unrevoked":
                case "user_delete":
                case "user_suspend":
                    return;
                case "follow":
                    if (eventData.Target.Id == this.UserId)
                    {
                        if (!this.followerId.Contains(eventData.Source.Id)) this.followerId.Add(eventData.Source.Id);
                    }
                    else
                    {
                        return;    //Block後のUndoをすると、SourceとTargetが逆転したfollowイベントが帰ってくるため。
                    }
                    evt.Target = "";
                    break;
                case "unfollow":
                    evt.Target = "@" + eventData.Target.ScreenName;
                    break;
                case "favorited_retweet":
                case "retweeted_retweet":
                    return;
                case "favorite":
                case "unfavorite":
                    tweetEvent = message.ParseTargetObjectAs<TwitterStatusCompat>();
                    tweet = tweetEvent.TargetObject.Normalize();
                    evt.Target = "@" + tweet.User.ScreenName + ":" + WebUtility.HtmlDecode(tweet.FullText);
                    evt.Id = tweet.Id;

                    if (SettingManager.Common.IsRemoveSameEvent)
                    {
                        if (this.StoredEvent.Any(ev => ev.Username == evt.Username && ev.Eventtype == evt.Eventtype && ev.Target == evt.Target))
                            return;
                    }

                    var tabinfo = TabInformations.GetInstance();

                    var statusId = tweet.Id;
                    if (!tabinfo.Posts.TryGetValue(statusId, out var post))
                        break;

                    if (eventData.Event == "favorite")
                    {
                        var favTab = tabinfo.GetTabByType(MyCommon.TabUsageType.Favorites);
                        favTab.AddPostQueue(post);

                        if (tweetEvent.Source.Id == this.UserId)
                        {
                            post.IsFav = true;
                        }
                        else if (tweetEvent.Target.Id == this.UserId)
                        {
                            post.FavoritedCount++;

                            if (SettingManager.Common.FavEventUnread)
                                tabinfo.SetReadAllTab(post.StatusId, read: false);
                        }
                    }
                    else // unfavorite
                    {
                        if (tweetEvent.Source.Id == this.UserId)
                        {
                            post.IsFav = false;
                        }
                        else if (tweetEvent.Target.Id == this.UserId)
                        {
                            post.FavoritedCount = Math.Max(0, post.FavoritedCount - 1);
                        }
                    }
                    break;
                case "quoted_tweet":
                    if (evt.IsMe) return;

                    tweetEvent = message.ParseTargetObjectAs<TwitterStatusCompat>();
                    tweet = tweetEvent.TargetObject.Normalize();
                    evt.Target = "@" + tweet.User.ScreenName + ":" + WebUtility.HtmlDecode(tweet.FullText);
                    evt.Id = tweet.Id;

                    if (SettingManager.Common.IsRemoveSameEvent)
                    {
                        if (this.StoredEvent.Any(ev => ev.Username == evt.Username && ev.Eventtype == evt.Eventtype && ev.Target == evt.Target))
                            return;
                    }
                    break;
                case "list_member_added":
                case "list_member_removed":
                case "list_created":
                case "list_destroyed":
                case "list_updated":
                case "list_user_subscribed":
                case "list_user_unsubscribed":
                    var listEvent = message.ParseTargetObjectAs<TwitterList>();
                    evt.Target = listEvent.TargetObject.FullName;
                    break;
                case "block":
                    if (!TabInformations.GetInstance().BlockIds.Contains(eventData.Target.Id)) TabInformations.GetInstance().BlockIds.Add(eventData.Target.Id);
                    evt.Target = "";
                    break;
                case "unblock":
                    if (TabInformations.GetInstance().BlockIds.Contains(eventData.Target.Id)) TabInformations.GetInstance().BlockIds.Remove(eventData.Target.Id);
                    evt.Target = "";
                    break;
                case "user_update":
                    evt.Target = "";
                    break;
                
                // Mute / Unmute
                case "mute":
                    evt.Target = "@" + eventData.Target.ScreenName;
                    if (!TabInformations.GetInstance().MuteUserIds.Contains(eventData.Target.Id))
                    {
                        TabInformations.GetInstance().MuteUserIds.Add(eventData.Target.Id);
                    }
                    break;
                case "unmute":
                    evt.Target = "@" + eventData.Target.ScreenName;
                    if (TabInformations.GetInstance().MuteUserIds.Contains(eventData.Target.Id))
                    {
                        TabInformations.GetInstance().MuteUserIds.Remove(eventData.Target.Id);
                    }
                    break;

                default:
                    MyCommon.TraceOut("Unknown Event:" + evt.Event + Environment.NewLine + message.Json);
                    break;
            }
            this.StoredEvent.Insert(0, evt);

            this.UserStreamEventReceived?.Invoke(this, new UserStreamEventReceivedEventArgs(evt));
        }

        private void userStream_Started()
            => this.UserStreamStarted?.Invoke(this, EventArgs.Empty);

        private void userStream_Stopped()
            => this.UserStreamStopped?.Invoke(this, EventArgs.Empty);

        public bool UserStreamActive
            => this.userStreamConnector != null && this.userStreamConnector.IsStreamActive;

        public void StartUserStream()
        {
            var replies = this.AllAtReply ? "all" : null;
            var streamObservable = this.Api.UserStreams(replies, this.TrackWord);
            var newConnector = new StreamAutoConnector(streamObservable);

            newConnector.MessageReceived += userStream_MessageReceived;
            newConnector.Started += userStream_Started;
            newConnector.Stopped += userStream_Stopped;

            newConnector.Start();

            var oldConnector = Interlocked.Exchange(ref this.userStreamConnector, newConnector);
            oldConnector?.Dispose();
        }

        public void StopUserStream()
        {
            var oldConnector = Interlocked.Exchange(ref this.userStreamConnector, null);
            oldConnector?.Dispose();
        }

        public void ReconnectUserStream()
        {
            if (this.userStreamConnector != null)
            {
                this.StartUserStream();
            }
        }

        private class StreamAutoConnector : IDisposable
        {
            private readonly TwitterStreamObservable streamObservable;

            public bool IsStreamActive { get; private set; }
            public bool IsDisposed { get; private set; }

            public event Action<ITwitterStreamMessage> MessageReceived;
            public event Action Stopped;
            public event Action Started;

            private Task streamTask;
            private CancellationTokenSource streamCts = new CancellationTokenSource();

            public StreamAutoConnector(TwitterStreamObservable streamObservable)
                => this.streamObservable = streamObservable;

            public void Start()
            {
                var cts = new CancellationTokenSource();

                this.streamCts = cts;
                this.streamTask = Task.Run(async () =>
                {
                    try
                    {
                        await this.StreamLoop(cts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                });
            }

            public void Stop()
            {
                this.streamCts?.Cancel();

                // streamTask の完了を待たずに IsStreamActive を false にセットする
                this.IsStreamActive = false;
                this.Stopped?.Invoke();
            }

            private async Task StreamLoop(CancellationToken cancellationToken)
            {
                TimeSpan sleep = TimeSpan.Zero;
                for (; ; )
                {
                    if (sleep != TimeSpan.Zero)
                    {
                        await Task.Delay(sleep, cancellationToken)
                            .ConfigureAwait(false);
                        sleep = TimeSpan.Zero;
                    }

                    if (!MyCommon.IsNetworkAvailable())
                    {
                        sleep = TimeSpan.FromSeconds(30);
                        continue;
                    }

                    this.IsStreamActive = true;
                    this.Started?.Invoke();

                    try
                    {
                        await this.streamObservable.ForEachAsync(
                            x => this.MessageReceived?.Invoke(x),
                            cancellationToken);

                        // キャンセルされていないのにストリームが終了した場合
                        sleep = TimeSpan.FromSeconds(30);
                    }
                    catch (TwitterApiException ex) when (ex.StatusCode == HttpStatusCode.Gone)
                    {
                        // UserStreams停止によるエラーの場合は長めに間隔を開ける
                        sleep = TimeSpan.FromMinutes(10);
                    }
                    catch (TwitterApiException) { sleep = TimeSpan.FromSeconds(30); }
                    catch (IOException) { sleep = TimeSpan.FromSeconds(30); }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw;

                        // cancellationToken によるキャンセルではない（＝タイムアウトエラー）
                        sleep = TimeSpan.FromSeconds(30);
                    }
                    catch (Exception ex)
                    {
                        MyCommon.ExceptionOut(ex);
                        sleep = TimeSpan.FromSeconds(30);
                    }
                    finally
                    {
                        this.IsStreamActive = false;
                        this.Stopped?.Invoke();
                    }
                }
            }

            public void Dispose()
            {
                if (this.IsDisposed)
                    return;

                this.IsDisposed = true;

                this.Stop();

                this.Started = null;
                this.Stopped = null;
                this.MessageReceived = null;
            }
        }
#endregion

#region "IDisposable Support"
        private bool disposedValue; // 重複する呼び出しを検出するには

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.StopUserStream();
                }
            }
            this.disposedValue = true;
        }

        //protected Overrides void Finalize()
        //{
        //    // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
        //    Dispose(false)
        //    MyBase.Finalize()
        //}

        // このコードは、破棄可能なパターンを正しく実装できるように Visual Basic によって追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            GC.SuppressFinalize(this);
        }
#endregion
    }

    public class PostDeletedEventArgs : EventArgs
    {
        public long StatusId { get; }

        public PostDeletedEventArgs(long statusId)
            => this.StatusId = statusId;
    }

    public class UserStreamEventReceivedEventArgs : EventArgs
    {
        public Twitter.FormattedEvent EventData { get; }

        public UserStreamEventReceivedEventArgs(Twitter.FormattedEvent eventData)
            => this.EventData = eventData;
    }
}
