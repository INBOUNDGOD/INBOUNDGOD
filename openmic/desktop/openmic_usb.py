"""USB direct-connection helper.

Over USB there's no IP: we tunnel the phone's TCP port to the PC's localhost
with `adb forward`, then connect to 127.0.0.1. This is lower latency and immune
to Wi-Fi jitter (the audio is already lossless PCM either way).

Requires Android platform-tools (`adb`) on PATH, USB debugging enabled on the
phone, and the phone authorized for this PC.
"""

import shutil
import subprocess

import openmic_proto as p


def adb_available() -> bool:
    return shutil.which("adb") is not None


def forward(port: int = p.DEFAULT_PORT, adb: str = "adb") -> str:
    """Run `adb forward tcp:<port> tcp:<port>`. Returns "" on success or an
    error message describing what went wrong."""
    if not shutil.which(adb):
        return ("adb (Android platform-tools) not found on PATH. Install it and "
                "enable USB debugging on the phone.")
    try:
        devs = subprocess.run([adb, "devices"], capture_output=True, text=True,
                              timeout=10)
        lines = [l for l in devs.stdout.splitlines()[1:] if l.strip()
                 and "device" in l]
        if not lines:
            return ("No USB device detected by adb. Plug in the phone, enable "
                    "USB debugging, and accept the authorization prompt.")
        r = subprocess.run([adb, "forward", f"tcp:{port}", f"tcp:{port}"],
                           capture_output=True, text=True, timeout=10)
        if r.returncode != 0:
            return f"adb forward failed: {r.stderr.strip() or r.stdout.strip()}"
        return ""
    except Exception as e:  # noqa: BLE001
        return f"adb error: {e}"


def remove(port: int = p.DEFAULT_PORT, adb: str = "adb") -> None:
    if shutil.which(adb):
        try:
            subprocess.run([adb, "forward", "--remove", f"tcp:{port}"],
                           capture_output=True, timeout=10)
        except Exception:  # noqa: BLE001
            pass
