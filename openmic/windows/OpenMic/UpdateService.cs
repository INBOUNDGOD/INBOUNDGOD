using Velopack;
using Velopack.Sources;

namespace OpenMic;

/// <summary>
/// Auto-update via Velopack. On launch the installed app checks the project's
/// GitHub Releases; if a newer version is published it downloads it in the
/// background and can apply it on restart — no manual reinstall.
///
/// To ship an update: bump &lt;Version&gt; in the csproj, run the vpk packaging
/// step (see windows/README.md), and publish the result as a new GitHub Release.
/// </summary>
public sealed class UpdateService
{
    // The repo whose Releases hold the published versions.
    private const string RepoUrl = "https://github.com/INBOUNDGOD/INBOUNDGOD";

    private readonly UpdateManager _mgr;
    private UpdateInfo? _pending;

    public UpdateService()
        => _mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));

    /// <summary>True only when launched from a Velopack install (not a dev run).</summary>
    public bool IsInstalled => _mgr.IsInstalled;

    /// <summary>
    /// Check for and download a newer release. Returns the new version string if
    /// one is ready to apply, otherwise null. Never throws (offline-safe).
    /// </summary>
    public async Task<string?> CheckAndPrepareAsync()
    {
        try
        {
            if (!_mgr.IsInstalled) return null;
            _pending = await _mgr.CheckForUpdatesAsync();
            if (_pending == null) return null;
            await _mgr.DownloadUpdatesAsync(_pending);
            return _pending.TargetFullRelease.Version.ToString();
        }
        catch
        {
            return null; // no network / no releases yet / etc. — just keep running
        }
    }

    /// <summary>Apply the downloaded update and relaunch the app.</summary>
    public void ApplyAndRestart()
    {
        if (_pending != null) _mgr.ApplyUpdatesAndRestart(_pending);
    }
}
