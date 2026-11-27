/**
 * EchoStem Now Playing — browser half.
 *
 * The EchoStem desktop app exposes a very small loopback-only JSON bridge
 * (127.0.0.1:38721) while "DSH plugin sync support" is enabled in its
 * settings. This module polls that bridge and draws an in-flow player in the
 * sidebar foot, directly above Settings. It deliberately does not use the
 * frame-wide overlay: the player is part of the left panel.
 *
 * The visual language follows EchoStem's micro player: the album art is on the
 * left, the artist / large light title / album stack sits beside it, a
 * full-width two-line lyric row follows, then the playback controls and a thin
 * accent progress line. Colours come from the running EchoStem instance via
 * the state payload's `theme` object, so the widget matches the player's
 * current light/dark theme and accent colour.
 */
window.__ModuleLoader__.load({
  id: '@local/echostem-sync',
  factory(require) {
    const React = require('react');
    const h = React.createElement;
    const useState = React.useState;
    const useEffect = React.useEffect;
    const useRef = React.useRef;

    const API = 'http://127.0.0.1:38721';
    const POLL_MS = 1000;

    const en = {
      previous: 'Previous track',
      play: 'Play',
      pause: 'Pause',
      next: 'Next track',
      unknownArtist: 'Unknown artist',
      unknownAlbum: 'Unknown album',
      seek: 'Seek',
    };

    const zh = {
      previous: '上一首',
      play: '播放',
      pause: '暂停',
      next: '下一首',
      unknownArtist: '未知歌手',
      unknownAlbum: '未知专辑',
      seek: '跳转',
    };

    function formatTime(milliseconds) {
      if (!isFinite(milliseconds) || milliseconds <= 0) return '0:00';
      const total = Math.floor(milliseconds / 1000);
      const minutes = Math.floor(total / 60);
      const seconds = total % 60;
      return minutes + ':' + String(seconds).padStart(2, '0');
    }

    function strokeIcon(size, paths) {
      return h('svg', {
        viewBox: '0 0 24 24',
        width: size,
        height: size,
        fill: 'none',
        stroke: 'currentColor',
        strokeWidth: 1.8,
        strokeLinecap: 'round',
        strokeLinejoin: 'round',
        'aria-hidden': true,
      }, paths.map((path, index) => h('path', { key: index, d: path })));
    }

    function filledIcon(size, path) {
      return h('svg', {
        viewBox: '0 0 24 24',
        width: size,
        height: size,
        fill: 'currentColor',
        'aria-hidden': true,
      }, h('path', { d: path }));
    }

    function iconPrevious(size) {
      return strokeIcon(size, ['M6 6v12', 'M18 6l-9 6 9 6z']);
    }

    function iconNext(size) {
      return strokeIcon(size, ['M18 6v12', 'M6 6l9 6-9 6z']);
    }

    function iconPlay(size) {
      return filledIcon(size, 'M8 5l11 7-11 7z');
    }

    function iconPause(size) {
      return h('svg', {
        viewBox: '0 0 24 24',
        width: size,
        height: size,
        fill: 'currentColor',
        'aria-hidden': true,
      },
        h('rect', { key: 'a', x: 6, y: 5, width: 4, height: 14, rx: 1 }),
        h('rect', { key: 'b', x: 14, y: 5, width: 4, height: 14, rx: 1 }));
    }

    function coverNode(src, visible, onError, className) {
      if (src && visible) {
        return h('img', {
          className: className,
          src: src,
          alt: '',
          draggable: false,
          onError: onError,
        });
      }

      return h('div', { className: 'esmp-placeholder ' + (className || '') },
        h('span', { className: 'esmp-placeholder-note', 'aria-hidden': true }, '\u266a'));
    }

    function splitLyric(text) {
      if (!text) return [];

      return text
        .split(/\r?\n/)
        .map((line) => line.trim())
        .filter((line) => line.length > 0);
    }

    function MiniPlayer(props) {
      const t = props.t;
      const wide = props.wide;

      const [state, setState] = useState(null);
      const [lyrics, setLyrics] = useState(null);
      const [clock, setClock] = useState(Date.now());
      const [coverVisible, setCoverVisible] = useState(true);

      const failures = useRef(0);
      const alive = useRef(true);

      useEffect(() => {
        alive.current = true;
        let timer = null;

        const poll = async () => {
          let delay = POLL_MS;

          try {
            const response = await fetch(API + '/api/state', { cache: 'no-store' });

            if (!response.ok) throw new Error('HTTP ' + response.status);

            const data = await response.json();

            if (!alive.current) return;

            failures.current = 0;

            if (!data || !data.ok || !data.hasTrack) {
              setState(null);
            } else {
              setState(Object.assign({}, data, { _receivedAt: Date.now() }));
              setClock(Date.now());
            }
          } catch (error) {
            if (!alive.current) return;
            failures.current += 1;
            delay = 2500;
            if (failures.current >= 1) setState(null);
          }

          if (alive.current) timer = setTimeout(poll, delay);
        };

        poll();

        return () => {
          alive.current = false;
          if (timer) clearTimeout(timer);
        };
      }, []);

      useEffect(() => {
        if (!state || !state.playing) return undefined;

        const id = setInterval(() => setClock(Date.now()), 250);
        return () => clearInterval(id);
      }, [state ? state.playing : false]);

      const lyricsVersion = state ? state.lyricsVersion : null;

      useEffect(() => {
        if (!lyricsVersion) {
          setLyrics(null);
          return undefined;
        }

        let cancelled = false;
        setLyrics(null);

        fetch(API + '/api/lyrics', { cache: 'no-store' })
          .then((response) => (response.ok ? response.json() : null))
          .then((data) => {
            if (cancelled || !data) return;
            if (data.version && data.version !== lyricsVersion) return;

            const lines = Array.isArray(data.lines) ? data.lines.slice() : [];
            lines.sort((a, b) => a.t - b.t);
            setLyrics(lines);
          })
          .catch(() => { /* the next track change retries */ });

        return () => { cancelled = true; };
      }, [lyricsVersion]);

      const coverSrc = state && state.coverVersion
        ? API + '/api/cover?v=' + encodeURIComponent(state.coverVersion)
        : null;

      useEffect(() => {
        setCoverVisible(true);
      }, [coverSrc]);

      if (!wide || !state || !state.hasTrack) return null;

      const track = state.track || {};
      const title = track.title || '';
      const artist = track.artist || t('unknownArtist');
      const album = track.album || t('unknownAlbum');

      let position = state.positionMs || 0;

      if (state.playing && state._receivedAt) {
        position = Math.min(state.durationMs || Infinity, position + (clock - state._receivedAt));
      }

      if (!isFinite(position) || position < 0) position = 0;

      const progress = state.durationMs > 0
        ? Math.max(0, Math.min(1, position / state.durationMs))
        : 0;

      let currentLyric = null;

      if (lyrics && lyrics.length) {
        for (let index = 0; index < lyrics.length; index += 1) {
          const line = lyrics[index];
          if (!line.timed) continue;
          if (line.t <= position + 300) currentLyric = line.text;
          else break;
        }
      }

      currentLyric = currentLyric && currentLyric.trim() ? currentLyric.trim() : null;

      const lyricLines = splitLyric(currentLyric);
      const originalLyric = lyricLines[0] || null;
      const translatedLyric = lyricLines.length > 1
        ? lyricLines.slice(1).join(' ')
        : null;

      const theme = state.theme || {};
      const themeStyle = {
        '--esmp-primary': theme.primaryText || 'var(--dsw-alias-label-primary)',
        '--esmp-secondary': theme.secondaryText || 'var(--dsw-alias-label-secondary)',
        '--esmp-accent': theme.accent || 'var(--dsw-alias-brand-primary)',
        '--esmp-separator': theme.separator || 'var(--dsw-alias-border-l1)',
        '--esmp-track': theme.progressTrack || 'var(--dsw-alias-border-l1)',
      };

      const send = (action, value) => {
        const body = value === undefined ? { action } : { action, value };

        fetch(API + '/api/command', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        }).catch(() => { /* the state poll reports the disconnect */ });
      };

      const togglePlay = () => {
        if (state.playing) {
          setState(Object.assign({}, state, { playing: false, status: 'paused' }));
          send('pause');
        } else {
          setState(Object.assign({}, state, { playing: true, status: 'playing', _receivedAt: Date.now() }));
          setClock(Date.now());
          send('play');
        }
      };

      const seek = (event) => {
        if (!state.durationMs) return;

        const rect = event.currentTarget.getBoundingClientRect();
        if (!rect.width) return;

        const ratio = Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width));
        setState(Object.assign({}, state, { positionMs: ratio * state.durationMs, _receivedAt: Date.now() }));
        setClock(Date.now());
        send('seek', ratio);
      };

      const seekByKeyboard = (event) => {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
        if (!state.durationMs) return;

        event.preventDefault();

        const delta = event.key === 'ArrowLeft' ? -5000 : 5000;
        const ratio = Math.max(0, Math.min(1, (position + delta) / state.durationMs));

        setState(Object.assign({}, state, { positionMs: ratio * state.durationMs, _receivedAt: Date.now() }));
        setClock(Date.now());
        send('seek', ratio);
      };

      return h('div', { className: 'esmp-root', style: themeStyle },
        h('style', null, CSS),

        h('div', { className: 'esmp-player' },
          h('div', { className: 'esmp-content' },
            h('div', { className: 'esmp-head' },
              h('div', { className: 'esmp-cover' },
                coverNode(coverSrc, coverVisible, () => setCoverVisible(false), 'esmp-cover-img')),

              h('div', { className: 'esmp-info' },
                h('div', { className: 'esmp-artist', title: artist }, artist),
                h('div', { className: 'esmp-title', title: title }, title),
                h('div', { className: 'esmp-album', title: album }, album))),

            h('div', { className: 'esmp-controls' },
              h('button', {
                type: 'button',
                className: 'esmp-btn',
                onClick: () => send('previous'),
                title: t('previous'),
                'aria-label': t('previous'),
              }, iconPrevious(17)),

              h('button', {
                type: 'button',
                className: 'esmp-btn esmp-btn-play',
                onClick: togglePlay,
                title: state.playing ? t('pause') : t('play'),
                'aria-label': state.playing ? t('pause') : t('play'),
              }, state.playing ? iconPause(16) : iconPlay(16)),

              h('button', {
                type: 'button',
                className: 'esmp-btn',
                onClick: () => send('next'),
                title: t('next'),
                'aria-label': t('next'),
              }, iconNext(17)))),

          h('div', {
            className: 'esmp-progress',
            role: 'slider',
            tabIndex: 0,
            'aria-label': t('seek'),
            'aria-valuemin': 0,
            'aria-valuemax': 100,
            'aria-valuenow': Math.round(progress * 100),
            title: formatTime(position) + ' / ' + formatTime(state.durationMs),
            onClick: seek,
            onKeyDown: seekByKeyboard,
          },
            h('div', { className: 'esmp-progress-track' },
              h('div', { className: 'esmp-progress-fill', style: { width: (progress * 100) + '%' } }))),

          h('div', { className: 'esmp-lyrics', title: currentLyric || '' },
            h('div', { className: 'esmp-lyric-line' + (originalLyric ? '' : ' esmp-empty') },
              originalLyric || '\u00a0'),
            h('div', { className: 'esmp-lyric-line' + (translatedLyric ? '' : ' esmp-empty') },
              translatedLyric || '\u00a0'))));
    }

    const CSS = [
      '.esmp-root{flex:1 1 0;min-width:0;box-sizing:border-box;padding:6px 0 6px;border-top:1px solid var(--esmp-separator);',
      'font-family:"Segoe UI","Microsoft YaHei UI",sans-serif;animation:esmp-in .18s ease-out}',
      '@keyframes esmp-in{from{opacity:0;transform:translateY(4px)}to{opacity:1;transform:none}}',
      '.esmp-player{width:100%;box-sizing:border-box;color:var(--esmp-primary)}',
      '.esmp-content{padding:0 12px 4px}',
      '.esmp-head{display:flex;align-items:center;gap:12px;min-width:0}',
      '.esmp-cover{width:64px;height:64px;flex:0 0 64px;border-radius:6px;overflow:hidden}',
      '.esmp-cover-img{width:100%;height:100%;object-fit:cover;display:block;border-radius:6px}',
      '.esmp-placeholder{width:100%;height:100%;display:flex;align-items:center;justify-content:center;',
      'color:var(--esmp-secondary);background:color-mix(in srgb,var(--esmp-primary) 6%,transparent)}',
      '.esmp-placeholder-note{font-size:22px;line-height:1;opacity:.75}',
      '.esmp-info{flex:1;min-width:0;display:flex;flex-direction:column;gap:1px;padding-top:1px}',
      '.esmp-artist{font-size:11px;line-height:1.3;color:var(--esmp-secondary);white-space:nowrap;',
      'overflow:hidden;text-overflow:ellipsis}',
      '.esmp-title{font-size:18px;font-weight:300;line-height:1.18;letter-spacing:-.2px;',
      'color:var(--esmp-primary);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
      '.esmp-album{font-size:10px;line-height:1.3;color:var(--esmp-secondary);white-space:nowrap;',
      'overflow:hidden;text-overflow:ellipsis;margin-top:1px}',
      '.esmp-lyrics{margin:4px 12px 0;height:32px;display:flex;flex-direction:column;gap:0;overflow:hidden}',
      '.esmp-lyric-line{font-size:11px;line-height:1.4;color:var(--esmp-accent);white-space:nowrap;',
      'overflow:hidden;text-overflow:ellipsis}',
      '.esmp-lyric-line.esmp-empty{visibility:hidden}',
      '.esmp-controls{margin-top:4px;height:28px;display:flex;align-items:center;justify-content:flex-start;gap:4px}',
      '.esmp-btn{position:relative;width:24px;height:24px;padding:0;border:0;border-radius:6px;',
      'background:transparent;color:var(--esmp-primary);display:grid;place-items:center;cursor:pointer;flex:0 0 auto}',
      '.esmp-btn:hover{background:color-mix(in srgb,var(--esmp-primary) 8%,transparent)}',
      '.esmp-btn:focus-visible{outline:2px solid var(--esmp-accent);outline-offset:1px}',
      '.esmp-btn-play{width:28px;height:28px;color:var(--esmp-primary)}',
      '.esmp-progress{height:4px;display:flex;align-items:center;cursor:pointer}',
      '.esmp-progress:focus-visible{outline:2px solid var(--esmp-accent);outline-offset:-2px}',
      '.esmp-progress-track{height:2px;width:100%;overflow:hidden;background:var(--esmp-track);transition:height .12s ease}',
      '.esmp-progress-fill{height:100%;background:var(--esmp-accent);transition:width .25s linear}',
      '.esmp-progress:hover .esmp-progress-track{height:4px}',
    ].join('');

    function apply(ctx) {
      ctx.effect(() => ctx.locale.register('echostem.sync', { zh, en }), 'echostem: player labels');

      const t = ctx.locale.bind('echostem.sync');

      ctx.slots.inject('sidebar.footer.action', () => ctx.slots.register({
        name: 'sidebar.footer.action',
        id: 'echostem-mini-player',
        order: 10,
      }, (props) => h(MiniPlayer, { t, wide: props.wide })));
    }

    return {
      inject: ['slots', 'locale'],
      apply,
    };
  },
});
