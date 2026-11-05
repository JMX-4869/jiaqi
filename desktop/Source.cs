using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace JiaQi
{
    /// 一条用户添加的抓取源。用户可能贴的是某个子页面，
    /// 这里统一规范化成主站（scheme + host），抓取时从主站找子页面。
    public class CrawlSource
    {
        public string input = "";     // 用户原样输入的
        public string origin = "";    // 规范化后的主站，如 https://jyt.hebei.gov.cn
        public string name = "";      // 站点名，从页面标题或域名取
        public string status = "";    // 空=待抓 / ok=抓过 / fail=失败
        public string lastRun = "";
        public int found;             // 上次抓到几条日期
        public string message = "";
    }

    public class CrawlHit
    {
        public string date = "";
        public string label = "";
        public string pageUrl = "";
        public string pageTitle = "";
        public string ctx = "";
    }

    public static class SourceStore
    {
        public static List<CrawlSource> Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return new List<CrawlSource>();
                var txt = File.ReadAllText(path, Encoding.UTF8);
                var ser = new JavaScriptSerializer();
                var raw = ser.DeserializeObject(txt) as object[];
                var list = new List<CrawlSource>();
                if (raw == null) return list;
                foreach (var o in raw)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    list.Add(new CrawlSource
                    {
                        input = S(d, "input"),
                        origin = S(d, "origin"),
                        name = S(d, "name"),
                        status = S(d, "status"),
                        lastRun = S(d, "lastRun"),
                        found = I(d, "found"),
                        message = S(d, "message"),
                    });
                }
                return list;
            }
            catch { return new List<CrawlSource>(); }
        }

        public static void Save(string path, List<CrawlSource> list)
        {
            try
            {
                var arr = new List<object>();
                foreach (var s in list)
                {
                    arr.Add(new Dictionary<string, object>
                    {
                        {"input", s.input}, {"origin", s.origin}, {"name", s.name},
                        {"status", s.status}, {"lastRun", s.lastRun},
                        {"found", s.found}, {"message", s.message},
                    });
                }
                var ser = new JavaScriptSerializer();
                File.WriteAllText(path, ser.Serialize(arr), new UTF8Encoding(false));
            }
            catch { }
        }

        static string S(Dictionary<string, object> d, string k)
        {
            object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v) : "";
        }
        static int I(Dictionary<string, object> d, string k)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null) { try { return Convert.ToInt32(v); } catch { return 0; } }
            return 0;
        }
    }

    public static class Crawler
    {
        static Crawler()
        {
            // 政府网站证书链经常不全，直接不验证；TLS 也要放开，否则 https 站点全打不开
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
            ServicePointManager.DefaultConnectionLimit = 8;
        }

        /// 把用户贴的任意 URL 收成主站。https://jyt.hebei.gov.cn/col/xxx/a.html → https://jyt.hebei.gov.cn
        public static string NormalizeOrigin(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            url = url.Trim();
            if (!url.StartsWith("http")) url = "http://" + url;
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return "";
            string scheme = u.Scheme == "https" ? "https" : "http";
            return scheme + "://" + u.Host;
        }

        public static string HttpGet(string url, int timeoutMs = 15000)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) jiaqi/1.0";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.AllowAutoRedirect = true;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                {
                    if (stream == null) return "";
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        var bytes = ms.ToArray();
                        return Decode(bytes, resp.Headers["Content-Type"]);
                    }
                }
            }
            catch { return null; }
        }

        static string Decode(byte[] raw, string ctype)
        {
            var cands = new List<string>();
            var m = Regex.Match(ctype ?? "", "charset=([\\w-]+)", RegexOptions.IgnoreCase);
            if (m.Success) cands.Add(m.Groups[1].Value);
            var head = Encoding.ASCII.GetString(raw, 0, Math.Min(raw.Length, 4000));
            m = Regex.Match(head, "charset=[\"']?([\\w-]+)", RegexOptions.IgnoreCase);
            if (m.Success) cands.Add(m.Groups[1].Value);
            cands.Add("utf-8");
            cands.Add("gb18030");
            var seen = new HashSet<string>();
            foreach (var c in cands)
            {
                var k = c.ToLower();
                if (seen.Contains(k)) continue;
                seen.Add(k);
                try { return Encoding.GetEncoding(k).GetString(raw); }
                catch { }
            }
            return Encoding.UTF8.GetString(raw);
        }

        public static string StripTags(string html)
        {
            if (html == null) return "";
            var s = Regex.Replace(html, "(?is)<(script|style|noscript)[^>]*>.*?</\\1>", " ");
            s = Regex.Replace(s, "(?i)<br\\s*/?>", "\n");
            s = Regex.Replace(s, "(?i)</(p|div|li|tr|h\\d|td)>", "\n");
            s = Regex.Replace(s, "(?s)<[^>]+>", " ");
            s = s.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
            s = Regex.Replace(s, "[ \\t\\u3000]+", " ");
            s = Regex.Replace(s, "\\n\\s*\\n+", "\n");
            return s;
        }

        public class Link
        {
            public string href = "";
            public string text = "";
        }

        public static List<Link> ExtractLinks(string html, string baseUrl)
        {
            var list = new List<Link>();
            if (html == null) return list;
            var ms = Regex.Matches(html, "(?is)<a\\s[^>]*href\\s*=\\s*[\"']([^\"']+)[\"'][^>]*>(.*?)</a>");
            foreach (Match m in ms)
            {
                var href = m.Groups[1].Value.Trim();
                var text = Regex.Replace(m.Groups[2].Value, "(?s)<[^>]+>", "").Trim();
                if (href.Length == 0 || href.StartsWith("javascript") || href.StartsWith("#")) continue;
                string abs;
                try { abs = new Uri(new Uri(baseUrl), href).AbsoluteUri; }
                catch { continue; }
                list.Add(new Link { href = abs, text = text });
            }
            return list;
        }

        static readonly Regex DateFull = new Regex("(\\d{4})\\s*年\\s*(\\d{1,2})\\s*月\\s*(\\d{1,2})\\s*日");
        static readonly Regex DateMd = new Regex("(?<!\\d)(\\d{1,2})\\s*月\\s*(\\d{1,2})\\s*日");

        /// 从正文抽日期候选，按上下文认是寒假还是暑假。
        public static List<CrawlHit> ExtractDates(string text, string pageUrl, string pageTitle, int year = 2027)
        {
            var outList = new List<CrawlHit>();
            if (string.IsNullOrEmpty(text)) return outList;

            foreach (Match m in DateFull.Matches(text))
            {
                int y, mo, da;
                if (!int.TryParse(m.Groups[1].Value, out y)) continue;
                if (!int.TryParse(m.Groups[2].Value, out mo)) continue;
                if (!int.TryParse(m.Groups[3].Value, out da)) continue;
                if (mo < 1 || mo > 12 || da < 1 || da > 31) continue;
                var a = Math.Max(0, m.Index - 70);
                var b = Math.Min(text.Length, m.Index + m.Length + 70);
                var ctx = text.Substring(a, b - a).Replace("\n", " ");
                var afterTxt = text.Substring(m.Index + m.Length, Math.Min(40, text.Length - m.Index - m.Length));
                var label = GuessLabel(afterTxt);
                if (label.Length == 0) continue;
                outList.Add(new CrawlHit
                {
                    date = string.Format("{0:D4}-{1:D2}-{2:D2}", y, mo, da),
                    label = label, pageUrl = pageUrl, pageTitle = pageTitle, ctx = ctx,
                });
            }

            foreach (Match m in DateMd.Matches(text))
            {
                int mo, da;
                if (!int.TryParse(m.Groups[1].Value, out mo)) continue;
                if (!int.TryParse(m.Groups[2].Value, out da)) continue;
                if (mo < 1 || mo > 12 || da < 1 || da > 31) continue;
                var a = Math.Max(0, m.Index - 70);
                var b = Math.Min(text.Length, m.Index + m.Length + 70);
                var ctx = text.Substring(a, b - a).Replace("\n", " ");
                var afterTxt = text.Substring(m.Index + m.Length, Math.Min(40, text.Length - m.Index - m.Length));
                var label = GuessLabel(afterTxt);
                if (label.Length == 0) continue;
                outList.Add(new CrawlHit
                {
                    date = string.Format("{0:D4}-{1:D2}-{2:D2}", year, mo, da),
                    label = label, pageUrl = pageUrl, pageTitle = pageTitle, ctx = ctx,
                });
            }

            // 去重
            var seen = new HashSet<string>();
            var uniq = new List<CrawlHit>();
            foreach (var h in outList)
            {
                var key = h.date + "|" + h.label;
                if (seen.Contains(key)) continue;
                seen.Add(key);
                uniq.Add(h);
            }
            return uniq;
        }

        /// 只看日期**后面**那段，紧跟着「放寒假」才算；避免把「结束本学期工作」当放假
        static string GuessLabel(string after)
        {
            if (after.Contains("放寒假")) return "寒假开始";
            if (after.Contains("放暑假")) return "暑假开始";
            if (after.Contains("放春假")) return "春假开始";
            if (after.Contains("放秋假")) return "秋假开始";
            if (after.Contains("开学")) return "开学";
            return "";
        }

        /// 抓一个源：主站 → 找政务公开/通知公告栏目 → 栏目里的校历页面 → 抽日期。
        public static List<CrawlHit> Crawl(CrawlSource src, out string message, int maxPages = 10)
        {
            message = "";
            var hits = new List<CrawlHit>();
            if (src.origin.Length == 0) { message = "地址无效"; return hits; }

            // 用户贴的是具体页面时，先直接抓那一页；抓不到再回主站找
            if (src.input.Length > 0 && src.input != src.origin && src.input.StartsWith("http"))
            {
                var page = HttpGet(src.input);
                if (page != null)
                {
                    var ptm = Regex.Match(page, "(?is)<title[^>]*>(.*?)</title>");
                    string ptitle = ptm.Success ? ptm.Groups[1].Value.Trim() : src.input;
                    var direct = ExtractDates(StripTags(page), src.input, ptitle);
                    if (direct.Count > 0)
                    {
                        if (src.name.Length == 0) src.name = ptitle;
                        message = "直接从你给的页面抓到 " + direct.Count + " 条";
                        if (direct.Count > 60) direct.RemoveRange(60, direct.Count - 60);
                        return direct;
                    }
                }
            }

            var home = HttpGet(src.origin);
            if (home == null) { message = "主站打不开：" + src.origin; return hits; }

            var tm = Regex.Match(home, "(?is)<title[^>]*>(.*?)</title>");
            if (tm.Success && src.name.Length == 0)
                src.name = Regex.Replace(tm.Groups[1].Value, @"\s+", " ").Trim();

            var navKw = new Regex("政务公开|信息公开|通知公告|政策文件|公文|教育动态|公示公告|工作动态|文件");
            var docKw = new Regex("校历|放假|假期|日程|寒暑假|开学|寒假|暑假|春假|秋假");

            // 第一跳：首页 → 栏目页
            var first = ExtractLinks(home, src.origin);
            var hubs = new List<Link>();
            var seen = new HashSet<string>();
            foreach (var l in first)
            {
                if (l.href.IndexOf(src.origin, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (l.href.EndsWith(".jpg") || l.href.EndsWith(".png") || l.href.EndsWith(".pdf")) continue;
                if (!navKw.IsMatch(l.text)) continue;
                if (seen.Contains(l.href)) continue;
                seen.Add(l.href);
                hubs.Add(l);
                if (hubs.Count >= 4) break;
            }

            // 第一页本身也算候选（首页上直接就有校历链接的情况）
            var docLinks = new List<Link>();
            foreach (var l in first)
            {
                if (l.href.IndexOf(src.origin, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!docKw.IsMatch(l.text)) continue;
                if (seen.Contains(l.href)) continue;
                seen.Add(l.href);
                docLinks.Add(l);
            }

            // 第二跳：栏目页 → 校历页
            foreach (var h in hubs)
            {
                var hubHtml = HttpGet(h.href);
                if (hubHtml == null) continue;
                foreach (var l in ExtractLinks(hubHtml, h.href))
                {
                    if (l.href.IndexOf(src.origin, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (l.href.EndsWith(".jpg") || l.href.EndsWith(".png")) continue;
                    if (!docKw.IsMatch(l.text)) continue;
                    if (seen.Contains(l.href)) continue;
                    seen.Add(l.href);
                    docLinks.Add(l);
                    if (docLinks.Count >= maxPages * 2) break;
                }
                if (docLinks.Count >= maxPages * 2) break;
            }

            int pages = 0;
            foreach (var l in docLinks)
            {
                if (pages >= maxPages) break;
                pages++;
                var html = HttpGet(l.href);
                if (html == null) continue;
                var body = StripTags(html);
                var title = l.text.Length > 0 ? l.text : l.href;
                hits.AddRange(ExtractDates(body, l.href, title));
            }

            if (hits.Count == 0)
                message = "看了 " + hubs.Count + " 个栏目、" + pages + " 个页面，没找到带「寒假/暑假/开学」上下文的日期";
            else
                message = "看了 " + hubs.Count + " 个栏目、" + pages + " 个页面，找到 " + hits.Count + " 条";

            if (hits.Count > 60) hits.RemoveRange(60, hits.Count - 60);
            return hits;
        }
    }
}
