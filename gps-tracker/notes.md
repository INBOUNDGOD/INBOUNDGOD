# GPS tracker: take back control (project notes)

**Goal:** an old 2G + SIM-card GPS tracker the user built years ago. It was
configured to report its position to a vendor's "Chinese database" (a GPS
tracking platform). The user wants to regain control of their own device and
repurpose it — ideally pointing it at a server they own instead of the vendor
platform.

This is the user's own hardware. Everything below is about reconfiguring a
device they built and sending its data to their own server.

---

## What the hardware is (from the photos)

| Clue on the board | What it means |
|---|---|
| Silkscreen `MD302-V01 0511` | The tracker PCB model/revision. "0511" is a date/batch code. MD302 = a common cheap Chinese GPS tracker board. |
| `GT010` module + ceramic patch antenna + wire antenna | The GPS/GNSS receiver module. Patch antenna = GPS; the separate wire is the GSM (cellular) antenna. |
| Shielded metal can with red/black battery wires | The **2G GSM modem** (almost certainly a SIMCom **SIM800/SIM900** family or a clone). This is what dials out over the cell network. |
| Combined SIM + microSD slot | SIM = the cellular line. microSD = optional offline logging of positions when there's no signal. |
| Pads `VBAT GND CLK RST DAT` | A **programming / debug header** for the on-board microcontroller (CLK/DAT/RST is a single-wire or SWD-style debug interface). Useful later for reading or reflashing firmware, but not needed for the easy path. |

**So the architecture is the standard one for these units:** a small MCU +
a 2G GSM modem + a GPS module. The MCU reads GPS, and tells the modem to open
a GPRS (2G data) connection to a server IP/port and send position reports in a
fixed protocol. It's configured almost entirely by **SMS text commands**.

## What "linked to a Chinese database" actually means

These trackers ship pre-pointed at the maker's **GPS tracking platform** — a
server + web/app dashboard (common ones: gpsui.net, 998gps / whatsgps,
gps903.net, alittlecar, etc.). You register the device's **IMEI** on their
site/app and watch it on a map. The device just sends packets to that server's
IP and port over the SIM's data connection.

Two stored settings make that happen, both changeable by SMS:
1. **APN** — the SIM carrier's data gateway (so the modem can get online).
2. **Server IP + port** (or a domain) — where reports are sent.

"Taking it back" = changing #2 (and #1) to point at **your** server.

## The protocol

Cheap trackers speak one of a handful of fixed binary/text protocols:
**GT06**, **TK103 / GPS103 (Coban)**, **H02**, **Xexun**, etc. We need to know
which one this firmware speaks, because:
- the **SMS command words differ** per firmware, and
- your own server has to decode the same protocol.

The MD302 / GT010 generation is most often **GT06** or **TK103-style**. We'll
confirm from the exact SMS command set it responds to (see below).

---

## The plan (easiest → hardest)

### Path A — reconfigure by SMS, receive on your own server (recommended)
No soldering. You keep the device as-is and just re-point it.

1. **Stand up a tracking server you own.** Best choice: **Traccar**
   (free, open source). It decodes ~200 of these cheap-tracker protocols
   (GT06, TK103, H02, etc.), each on its own port, and shows them on a map.
   It runs nicely on the user's **Synology NAS** (Docker: `traccar/traccar`).
2. **Make the NAS reachable from the cell network** — a public IP/DDNS +
   port-forward for the tracker's protocol port, or a tunnel. The tracker
   dials out, so the server just needs to be reachable on that one port.
3. **Put an active SIM in the tracker** and find/confirm its phone number.
4. **Text it the config commands** (admin password usually `123456`):
   set the **APN**, then set the **server IP/domain + port** to the NAS.
   Exact words depend on the firmware/protocol (confirm first).
5. **Register the device's IMEI** in Traccar. Watch it appear on the map.

Then "something cool" is wide open: geofence alerts, live sharing links,
history playback, Home Assistant integration, a phone notification when it
leaves/arrives somewhere — all from your own data, no third-party platform.

### Path B — read the current config first
Many firmwares answer a status/param SMS (e.g. `param#`, `status`,
`check123456`) with the APN, server and IMEI it's currently using. Good for
confirming the protocol and seeing where it was reporting, before changing
anything.

### Path C — read/reflash over the debug pads (`VBAT GND CLK RST DAT`)
Only if SMS config can't do what we want. Identify the MCU under the shield,
connect a programmer to those pads, dump the firmware, and either read its
stored server settings or flash custom firmware. More involved; keep as a
later option.

---

## The big practical blocker: 2G is being switched off

This is a **2G** device. Many carriers worldwide have already shut down 2G
(and some 3G). If the local network has no 2G, the modem simply can't connect,
no matter how it's configured. **This is the first thing to check.**
- If 2G is still up on some carrier/MVNO in the user's country → Path A works
  as written.
- If 2G is gone → options are a SIM/MVNO that still runs 2G, or swapping the
  modem for a 4G/LTE-Cat-M/NB-IoT module (a bigger hardware job), reusing the
  GPS module and MCU.

---

## What I need from the user to go further

1. **Country / carrier**, so I can check whether **2G** is still alive there
   and what the correct **APN** is.
2. **Is the SIM still active, and what's its phone number?** (Needed to text
   the tracker. If unknown, we can read it off the SIM in a phone.)
3. **What "something cool" means to you** — my default is: your data on your
   own Traccar server on the NAS, with live sharing + geofence alerts. Say if
   you had something else in mind (e.g. a specific use).
4. **Any memory of the old setup** — the platform name/website/app you used,
   the admin password if not `123456`, or the SMS commands you remember. Even
   a brand sticker photo helps pin the protocol.
5. **A clear photo of the metal-can modem's markings** (and the MCU if
   visible) to confirm SIM800 vs SIM900 vs other, and the protocol.

## Status
- **Started 2026-10-04.** Hardware identified from photos (MD302-V01 board,
  GT010 GPS module, 2G GSM modem, SIM+microSD). Plan drafted (Path A: Traccar
  on the NAS + SMS reconfigure). Waiting on the user's answers above,
  especially the **2G availability** check.
