// Checking GitHub for a newer MiniPad.
//
// The repository's main branch IS the release: MiniPad.exe and its source are
// committed there together, and the download page links the exe on main. So
// "the newest version" is the Version constant in About.cs on main, read from
// GitHub as one small plain-text file. There is no second version file to keep
// in step and forget.
//
// What leaves the machine: one HTTPS GET for that file, with a User-Agent of
// "MiniPad/<version>". No file names, no text, no settings, no identifier. The
// answer is only ever read for a version number - nothing in it is run, opened
// or shown - so a bad or hostile reply can at worst announce a version that
// does not exist. The links the notice opens are constants here, not anything
// the reply says.

using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

public static class Updates
{
    public const string Repository = "https://github.com/chromatic-media/MiniPad";
    public const string Download = Repository + "/raw/refs/heads/main/MiniPad.exe";
    public const string Changelog = Repository + "/blob/main/CHANGELOG.md";
    public const string VersionSource =
        "https://raw.githubusercontent.com/chromatic-media/MiniPad/main/About.cs";

    // How long between automatic checks. A check that fails is not counted, so
    // an offline laptop simply tries again later.
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    // About.cs is ~40 KB. Anything far past that is not the file we asked for.
    private const int MaxReply = 512 * 1024;

    public delegate void Finished(string latest, string error);

    // Runs off the UI thread, so a slow or dead connection never freezes typing.
    // `done` is called back on `owner`'s thread with either the version found on
    // GitHub or a plain-words reason it could not be found.
    public static void CheckAsync(Control owner, Finished done)
    {
        Thread worker = new Thread(delegate()
        {
            string latest = null;
            string error = null;
            try { latest = FetchLatestVersion(); }
            catch (WebException ex) { error = Describe(ex); }
            catch (Exception ex) { error = ex.Message; }

            try
            {
                if (!owner.IsDisposed && owner.IsHandleCreated)
                    owner.BeginInvoke(done, latest, error);
            }
            catch { }   // the window closed while we were asking; nobody to tell
        });
        worker.IsBackground = true;
        worker.Start();
    }

    private static string FetchLatestVersion()
    {
        // .NET 4's enum has no name for TLS 1.2, and GitHub accepts nothing older.
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(VersionSource);
        request.UserAgent = AppInfo.Name + "/" + AppInfo.Version;
        request.Timeout = 15000;
        request.ReadWriteTimeout = 15000;
        request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);

        string text;
        using (WebResponse response = request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        {
            byte[] buffer = new byte[MaxReply];
            int total = 0;
            int read;
            while (total < MaxReply && (read = stream.Read(buffer, total, MaxReply - total)) > 0)
                total += read;
            text = Encoding.UTF8.GetString(buffer, 0, total);
        }

        Match match = Regex.Match(text,
            @"const\s+string\s+Version\s*=\s*""([0-9]{1,4}(?:\.[0-9]{1,4}){1,3})""");
        if (!match.Success)
            throw new InvalidDataException("GitHub's copy did not say which version it is.");
        return match.Groups[1].Value;
    }

    private static string Describe(WebException ex)
    {
        if (ex.Status == WebExceptionStatus.NameResolutionFailure
            || ex.Status == WebExceptionStatus.ConnectFailure)
            return "This computer could not reach GitHub. Check the internet connection.";
        if (ex.Status == WebExceptionStatus.Timeout)
            return "GitHub did not answer in time.";
        return ex.Message;
    }

    // True when `latest` is a higher version than `current`. "1.12" and
    // "1.12.0" are the same version: System.Version alone would rank the
    // shorter one lower and announce an update to the version already running.
    public static bool IsNewer(string latest, string current)
    {
        try
        {
            return Parse(latest) > Parse(current);
        }
        catch
        {
            return false;   // an unreadable number is never news
        }
    }

    // System.Version refuses a bare "2", so a missing minor number becomes ".0".
    private static Version Parse(string text)
    {
        Version v = new Version(text.IndexOf('.') < 0 ? text + ".0" : text);
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }

    public static bool CheckIsDue
    {
        get
        {
            return Theme.UpdateCheck
                && (Theme.LastUpdateCheck == DateTime.MinValue
                    || DateTime.UtcNow - Theme.LastUpdateCheck >= Interval
                    || Theme.LastUpdateCheck > DateTime.UtcNow.AddMinutes(5));   // clock went back
        }
    }
}
