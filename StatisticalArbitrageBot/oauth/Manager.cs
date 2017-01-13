using System;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace StatisticalArbitrageBot.oauth
{
    public class Manager
    {

        public Manager()
        {
            _random = new Random();
            _params = new Dictionary<String, String>();
            _params["callback"] = "oob"; // presume "desktop" consumer
            _params["consumer_key"] = "";
            _params["consumer_secret"] = "";
            _params["timestamp"] = GenerateTimeStamp();
            _params["nonce"] = GenerateNonce();
            _params["signature_method"] = "HMAC-SHA1";
            _params["signature"] = "";
            _params["token"] = "";
            _params["token_secret"] = "";
            _params["version"] = "1.0";
        }

        public Manager(string consumerKey,
            string consumerSecret,
            string token,
            string tokenSecret)
            : this()
        {
            _params["consumer_key"] = consumerKey;
            _params["consumer_secret"] = consumerSecret;
            _params["token"] = token;
            _params["token_secret"] = tokenSecret;
        }

        public string this[string ix]
        {
            get
            {
                if (_params.ContainsKey(ix))
                    return _params[ix];
                throw new ArgumentException(ix);
            }
            set
            {
                if (!_params.ContainsKey(ix))
                    throw new ArgumentException(ix);
                _params[ix] = value;
            }
        }

        public string GenerateTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - _epoch;
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        public void NewRequest()
        {
            _params["nonce"] = GenerateNonce();
            _params["timestamp"] = GenerateTimeStamp();
        }

        public string GenerateNonce()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                int g = _random.Next(3);
                switch (g)
                {
                    case 0:
                        // lowercase alpha
                        sb.Append((char)(_random.Next(26) + 97), 1);
                        break;
                    default:
                        // numeric digits
                        sb.Append((char)(_random.Next(10) + 48), 1);
                        break;
                }
            }
            return sb.ToString();
        }

        public Dictionary<String, String> ExtractQueryParameters(string queryString)
        {
            if (queryString.StartsWith("?"))
                queryString = queryString.Remove(0, 1);

            var result = new Dictionary<String, String>();

            if (string.IsNullOrEmpty(queryString))
                return result;

            foreach (string s in queryString.Split('&'))
            {
                if (!string.IsNullOrEmpty(s) && !s.StartsWith("oauth_"))
                {
                    if (s.IndexOf('=') > -1)
                    {
                        string[] temp = s.Split('=');
                        result.Add(temp[0], temp[1]);
                    }
                    else
                        result.Add(s, string.Empty);
                }
            }

            return result;
        }

        public static string UrlEncode(string value)
        {
            var result = new System.Text.StringBuilder();
            foreach (char symbol in value)
            {
                if (unreservedChars.IndexOf(symbol) != -1)
                    result.Append(symbol);
                else
                    result.Append('%' + String.Format("{0:X2}", (int)symbol));
            }
            return result.ToString();
        }

        public static string unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

        public static string EncodeRequestParameters(ICollection<KeyValuePair<String, String>> p)
        {
            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<String, String> item in p.OrderBy(x => x.Key))
            {
                if (!String.IsNullOrEmpty(item.Value) &&
                    !item.Key.EndsWith("secret") && !item.Key.EndsWith("token")//)
                    && !item.Key.EndsWith("accountId") && !item.Key.EndsWith("clientOrderId")
                    && !item.Key.EndsWith("limitPrice") && !item.Key.EndsWith("quantity")
                    && !item.Key.EndsWith("symbol") && !item.Key.EndsWith("callOrPut")
                    && !item.Key.EndsWith("strikePrice") && !item.Key.EndsWith("expirationYear")
                    && !item.Key.EndsWith("expirationMonth") && !item.Key.EndsWith("expirationDay")
                    && !item.Key.EndsWith("orderAction") && !item.Key.EndsWith("priceType")
                    && !item.Key.EndsWith("orderTerm") && !item.Key.EndsWith("allOrNone")
                    && !item.Key.EndsWith("routingDestination"))
                {
                    sb.AppendFormat("oauth_{0}=\"{1}\", ",
                        item.Key,
                        UrlEncode(item.Value));
                    //   sb.Append(item.Key + "=" + UrlEncode(item.Value));
                }
                else if (!String.IsNullOrEmpty(item.Value) &&
                         item.Key.EndsWith("token"))
                {
                    sb.AppendFormat("oauth_{0}=\"{1}\", ",
                        item.Key, item.Value);
                }
                else if (!String.IsNullOrEmpty(item.Value) && !item.Key.EndsWith("secret") && !item.Key.EndsWith("token")
                         && (item.Key.EndsWith("accountId") || item.Key.EndsWith("clientOrderId")
                             || item.Key.EndsWith("limitPrice") || item.Key.EndsWith("quantity")
                             || item.Key.EndsWith("symbol") || item.Key.EndsWith("callOrPut")
                             || item.Key.EndsWith("strikePrice") || item.Key.EndsWith("expirationYear")
                             || item.Key.EndsWith("expirationMonth") || item.Key.EndsWith("expirationDay")
                             || item.Key.EndsWith("orderAction") || item.Key.EndsWith("priceType")
                             || item.Key.EndsWith("orderTerm") || item.Key.EndsWith("allOrNone")
                             || item.Key.EndsWith("routingDestination")))
                {
                    // sb.Append(","+item.Key+"="+ item.Value);
                    sb.AppendFormat("{0}=\"{1}\", ", item.Key, UrlEncode(item.Value));
                }
            }

            return sb.ToString().TrimEnd(' ').TrimEnd(',');
        }

        public OAuthResponse AcquireRequestToken(string uri, string method)
        {
            NewRequest();
            var authzHeader = GetAuthorizationHeader(uri, method);

            // prepare the token request
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.Headers.Add("Authorization", authzHeader);
            request.Method = method;
            using (var response = (System.Net.HttpWebResponse)request.GetResponse())
            {
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    var r = new OAuthResponse(reader.ReadToEnd());
                    this["token"] = r["oauth_token"];

                    // Sometimes the request_token URL gives us an access token,
                    // with no user interaction required. Eg, when prior approval
                    // has already been granted.
                    try
                    {
                        if (r["oauth_token_secret"] != null)
                            this["token_secret"] = r["oauth_token_secret"];
                    }
                    catch { }
                    return r;
                }
            }
        }

        public OAuthResponse AcquireAccessToken(string uri, string method, string pin)
        {
            NewRequest();
            _params["verifier"] = pin;
            var authzHeader = GetAuthorizationHeader(uri, method);

            // prepare the token request
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.Headers.Add("Authorization", authzHeader);
            request.Method = method;
            try
            {
                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                {
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                    {
                        var r = new OAuthResponse(reader.ReadToEnd());
                        this["token"] = r["oauth_token"];
                        this["token_secret"] = r["oauth_token_secret"];
                        return r;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                return null;
            }
        }

        public string GetAuthorizationHeader(string uri, string method)
        {
            return GetAuthorizationHeader(uri, method, null);
        }

        public string GetAuthorizationHeader(string uri, string method, string realm)
        {
            if (string.IsNullOrEmpty(this._params["consumer_key"]))
                throw new ArgumentNullException("consumer_key");

            if (string.IsNullOrEmpty(this._params["signature_method"]))
                throw new ArgumentNullException("signature_method");

            Sign(uri, method);

            var erp = EncodeRequestParameters(this._params);
            return (String.IsNullOrEmpty(realm))
                ? "OAuth " + erp
                : String.Format("OAuth realm=\"{0}\", ", realm) + erp;
        }

        public void Sign(string uri, string method)
        {
            var signatureBase = GetSignatureBase(uri, method);
            var hash = GetHash();

            byte[] dataBuffer = System.Text.Encoding.ASCII.GetBytes(signatureBase);
            byte[] hashBytes = hash.ComputeHash(dataBuffer);

            this["signature"] = Convert.ToBase64String(hashBytes);
        }

        public string GetSignatureBase(string url, string method)
        {
            // normalize the URI
            var uri = new Uri(url);
            var normUrl = string.Format("{0}://{1}", uri.Scheme, uri.Host);
            if (!((uri.Scheme == "http" && uri.Port == 80) ||
                  (uri.Scheme == "https" && uri.Port == 443)))
                normUrl += ":" + uri.Port;

            normUrl += uri.AbsolutePath;

            // the sigbase starts with the method and the encoded URI
            var sb = new System.Text.StringBuilder();
            sb.Append(method)
                .Append('&')
                .Append(UrlEncode(normUrl))
                .Append('&');

            // the parameters follow - all oauth params plus any params on
            // the uri
            // each uri may have a distinct set of query params
            var p = ExtractQueryParameters(uri.Query);
            // add all non-empty params to the "current" params
            foreach (var p1 in this._params)
            {
                // Exclude all oauth params that are secret or
                // signatures; any secrets should be kept to ourselves,
                // and any existing signature will be invalid.

                if (!String.IsNullOrEmpty(this._params[p1.Key]) &&
                    !p1.Key.EndsWith("secret")
                    && !p1.Key.EndsWith("signature")
                    && !p1.Key.EndsWith("accountId") && !p1.Key.EndsWith("clientOrderId")
                    && !p1.Key.EndsWith("limitPrice") && !p1.Key.EndsWith("quantity")
                    && !p1.Key.EndsWith("symbol") && !p1.Key.EndsWith("callOrPut")
                    && !p1.Key.EndsWith("strikePrice") && !p1.Key.EndsWith("expirationYear")
                    && !p1.Key.EndsWith("expirationMonth") && !p1.Key.EndsWith("expirationDay")
                    && !p1.Key.EndsWith("orderAction") && !p1.Key.EndsWith("priceType")
                    && !p1.Key.EndsWith("orderTerm") && !p1.Key.EndsWith("allOrNone")
                    && !p1.Key.EndsWith("routingDestination"))
                    p.Add("oauth_" + p1.Key, p1.Value);
                if (!String.IsNullOrEmpty(p1.Value) && !p1.Key.EndsWith("secret") && !p1.Key.EndsWith("token")
                    && (p1.Key.EndsWith("accountId") || p1.Key.EndsWith("clientOrderId")
                        || p1.Key.EndsWith("limitPrice") || p1.Key.EndsWith("quantity")
                        || p1.Key.EndsWith("symbol") || p1.Key.EndsWith("callOrPut")
                        || p1.Key.EndsWith("strikePrice") || p1.Key.EndsWith("expirationYear")
                        || p1.Key.EndsWith("expirationMonth") || p1.Key.EndsWith("expirationDay")
                        || p1.Key.EndsWith("orderAction") || p1.Key.EndsWith("priceType")
                        || p1.Key.EndsWith("orderTerm") || p1.Key.EndsWith("allOrNone")
                        || p1.Key.EndsWith("routingDestination")))
                {
                    p.Add(p1.Key, p1.Value);
                }

            }

            // concat+format all those params
            var sb1 = new System.Text.StringBuilder();
            foreach (KeyValuePair<String, String> item in p.OrderBy(x => x.Key))
            {
                // even "empty" params need to be encoded this way.
                sb1.AppendFormat("{0}={1}&", item.Key, item.Value);
            }

            // append the UrlEncoded version of that string to the sigbase
            sb.Append(UrlEncode(sb1.ToString().TrimEnd('&')));
            var result = sb.ToString();
            return result;
        }

        public HashAlgorithm GetHash()
        {
            if (this["signature_method"] != "HMAC-SHA1")
                throw new NotImplementedException();

            string keystring = string.Format("{0}&{1}",
                UrlEncode(this["consumer_secret"]),
                this["token_secret"]);
            var hmacsha1 = new HMACSHA1
            {
                Key = System.Text.Encoding.ASCII.GetBytes(keystring)
            };
            return hmacsha1;
        }

        public static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        public Dictionary<String, String> _params;
        public Random _random;
    }

    public class OAuthResponse
    {

        public string AllText { get; set; }
        public Dictionary<String, String> _params;

        public string this[string ix]
        {
            get
            {
                return _params[ix];
            }
        }

        public OAuthResponse(string alltext)
        {
            AllText = alltext;
            _params = new Dictionary<String, String>();
            var kvpairs = alltext.Split('&');
            foreach (var pair in kvpairs)
            {
                var kv = pair.Split('=');
                _params.Add(kv[0], kv[1]);
            }
        }
    }
}