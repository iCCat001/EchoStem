# EchoStem Now Playing

A DeepSeek Harness (DSH) Web UI plugin that embeds a compact EchoStem player in
the **left sidebar**, directly above the Settings entry, while EchoStem is
running.

It displays:

- the current song, artist and album;
- the album art served by EchoStem, on the left of the track information;
- previous / play-pause / next buttons;
- playback progress with a thin accent seek bar;
- a full-width synced lyric block below the progress line (one line when only
  the original is available, two lines when a translation is present), with a
  fixed two-line height so the widget never changes size between tracks.

The layout follows EchoStem's compact "micro" player: a left album cover, a
large light title with the artist and album beside it, a compact three-button
control bar, a thin progress line, and the lyric block below the progress. The
lyric font matches the rest of the metadata (11px), and the lyric area always
reserves exactly two lines, so a missing translation or missing lyrics leaves
empty space instead of moving the controls. A hairline at the top separates
the widget from the workspace/session list; because it is rendered in the
`sidebar.footer.action` slot, the footer grows upward and the workspace list
shrinks, so the player never covers list rows.

The widget uses the running player's text and accent colours from the
`/api/state` `theme` object, but it deliberately has no card background or
border: it sits directly on the sidebar surface. When the sidebar is collapsed
to the icon rail, the player is hidden.

## Enable the bridge in EchoStem

1. Open EchoStem → **Settings** → **Features**.
2. Turn on **DSH plugin sync support** (支持 DSH 插件同步).
3. Start playing a song.

EchoStem then listens only on `127.0.0.1:38721`. Turning the setting off stops
the listener; nothing is exposed while it is off. The port can be changed in
`Settings.xml` under the `Integration` namespace (`DshSyncPort`), but the
plugin's `client.js` currently uses the default port.

## How it works

EchoStem hosts a tiny loopback HTTP bridge:

| Endpoint | Purpose |
| --- | --- |
| `GET /api/state` | JSON playback state, track metadata, progress and versions |
| `GET /api/lyrics` | Parsed lyric lines with timestamps |
| `GET /api/cover` | Current album art bytes |
| `POST /api/command` | `playpause`, `play`, `pause`, `next`, `previous`, `seek`, `volume`, `mute`, `shuffle`, `loop` |

The browser half polls `/api/state` once per second, interpolates the progress
locally, and fetches lyrics when the track changes. The lyric line is computed
from the current playback time against the timed lyric lines, and is hidden
while paused, matching EchoStem's control-bar lyrics.

## Source location

This bundle lives in the `Dopamine.Plugins` project:

`Dopamine.Plugins\Plugins\Dsh\EchoStemSync`

`plugin.json` is the EchoStem plugin-catalog manifest. The DSH bundle files
(`package.json`, `cordis.patch.yml`, `index.js`, `client.js`, `locale/`,
`icon.svg`) remain in the same folder so the DSH Plugin Manager can install it
directly from this directory.
