using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Mime;
using System.Net;

namespace AtWillWebApp.Models
{
    public class TwitterClient
    {
        private string authorizeEndPoint = "https://twitter.com/i/oauth2/authorize";
        private string getTokenEndPoint = "https://api.twitter.com/2/oauth2/token";

        // HttpClientはアプリケーションの有効期間全体で再利用することを目的に作られている
        // そのため、つどHttpClientをnewしていると使用可能なソケットが枯渇する可能性があるため、
        // static readonlyで1回だけインスタンス化する
        // →シングルトンパターンでの生成もあり
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// 認可URLの生成
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="redirectUri"></param>
        /// <param name="scopes"></param>
        /// <param name="state"></param>
        /// <param name="challenge"></param>
        /// <returns></returns>
        public string ConstractAuthorizeUrl(string clientId, string redirectUri, IEnumerable<ScopeExtension.Scope> scopes, string state, string challenge)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);

            query.Add("response_type", "code");
            query.Add("client_id", clientId);
            query.Add("redirect_uri", redirectUri);
            // stateは認可コードを取得する際に一致しているか確認するために使用
            query.Add("state", state);
            query.Add("code_challenge", challenge);
            query.Add("code_challenge_method", "S256");

            var uriBuilder = new UriBuilder(authorizeEndPoint)
            {
                Query = query.ToString()
            };

            return uriBuilder.Uri.AbsoluteUri;
        }

        /// <summary>
        /// 認可コードの取得
        /// ※認可コードの期限は30秒のため、30秒以内にアクセストークンを取得する必要がある
        /// </summary>
        /// <param name="callbackedUri"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public string RetrieveAuthorizeCode(string callbackedUri, string state)
        {
            var uri = new Uri(callbackedUri);
            var query = HttpUtility.ParseQueryString(uri.Query);

            if(query.Get("state") != state)
            {
                throw new InvalidDataException("state is not valid.");
            }

            return query.Get("code");
        }

        /// <summary>
        /// アクセストークンの取得
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        /// <param name="redirectUri"></param>
        /// <param name="code"></param>
        /// <param name="verifier"></param>
        /// <returns></returns>
        public async Task<string> GetAccessTokenAsync(string clientId, string clientSecret, string redirectUri, string code, string verifier)
        {
            var parameters = new Dictionary<string, string>()
            {
                {"code", code},
                {"grant_type", "authorization_code" },
                {"redirect_uri", redirectUri },
                {"code_verifier", verifier }
            };

            var content = new FormUrlEncodedContent(parameters);
            var request = new HttpRequestMessage(HttpMethod.Post, getTokenEndPoint);

            // oAuth APIのエンドポイントになげるHTTPのヘッダーを生成
            var oAuthParameter = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", oAuthParameter);

            // oAuth APIのエンドポイントになげるHTTPのボディを設定
            request.Content = content;

            // APIへリクエストを投げる
            // 以下の問題を回避するために"ConfigureAwait(false)"を使用する
            // 非同期メソッドで Task を直接待機した場合、タスクを作成したスレッドで処理が継続する。
            // この動作はパフォーマンスの面で大きな負担が生じ、その結果 UI スレッドでデッドロックが発生する可能性がある。
            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            // APIからのレスポンスを読み込む
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return responseContent;
        }

        public async Task<string> RefreshTokenAsync()
        {
            return string.Empty;
        }

        public async Task<string> RevokeTokenAsync()
        {
            return string.Empty;
        }

        /// <summary>
        /// Tweetを読み込む
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<TweetList<GetTweet[]>> GetTweet(string accessToken, long id)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitter.com/2/tweets/{id}");

            // Tweetを読み込むAPIのエンドポイントに投げるHTTPのヘッダーを生成する
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // APIへリクエストを投げる
            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            // APIからのレスポンスを読み込む
            var responseContent = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            // APIレスポンスのJsonをデシリアライズ
            var tweetLists = JsonSerializer.Deserialize<TweetList<GetTweet[]>>(responseContent);

            if(tweetLists == null)
            {
                tweetLists = new TweetList<GetTweet[]>();
            }

            return tweetLists;
        }

        /// <summary>
        /// Tweetする
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public async Task<HttpStatusCode> PostTweet(string accessToken, string text)
        {
            // Tweet内容のJsonをシリアライズ化
            var tweetContent = new TweetList<PostTweet>();
            tweetContent.Data = new PostTweet { Text = text };

            var serializedJson = JsonSerializer.Serialize(tweetContent);
            var content = new StringContent(serializedJson, Encoding.UTF8, MediaTypeNames.Application.Json);

            // APIへ投げるリクエストの生成
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/2/tweets");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            // APIへリクエストを投げる
            var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            return response.StatusCode;
        }
        
    }

    /// <summary>
    /// Twitterで認可する操作
    /// oAuthで使用
    /// </summary>
    public static class ScopeExtension
    {
        public enum Scope
        {
            TweetRead,
            TweetWrite,
            TweetModerateWrite,
            UsersRead,
            FollowRead
        }

        private static Dictionary<Scope, string> ScopeValue { get; set; } = new Dictionary<Scope, string>
        {
            { Scope.TweetRead, "tweet.read"},
            { Scope.TweetWrite, "tweet.write"},
            { Scope.TweetModerateWrite, "tweet.moderate.write"},
            { Scope.UsersRead, "users.read"},
            { Scope.FollowRead, "follow.read"}
        };

        public static string GetValue(this Scope scope)
        {
            return ScopeValue[scope];
        }
    }

    /// <summary>
    /// Twitter APIとのやり取りで使用するJsonの(シリアライズ/デシリアライズ)クラス
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TweetList<T>
    {
        [JsonPropertyName ("data")]
        public T Data { get; set; }
    }

    public class GetTweet
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class PostTweet
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
