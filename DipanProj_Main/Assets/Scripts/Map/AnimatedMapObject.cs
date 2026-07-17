using UnityEngine;

/// <summary>
/// 動畫地上物的播放元件：用一個 SpriteRenderer 播放幀序列。由 <see cref="MapLoader"/> 建立時掛上並 Initialize。
///
/// 三種播放模式（由 .dipanmap 的 objects[] 決定，編輯器每實例可調）：
///   循環  ＝ pingPong=false, playOnce=false → 0→N-1 繞回 0（預設）。
///   乒乓  ＝ pingPong=true                  → 0→N-1→0 來回（AI 產圖首尾接不順時用，接縫不跳變）。
///   播一次＝ playOnce=true                   → 0→N-1 播一次就停在最後一幀（例：跪拜停在跪姿）。
///
/// 起播控制（給「靠旗標中途現身」用）：
///   startPlaying=false → 建好先暫停（藏起來時用），之後由 <see cref="MapObjectRevealer"/> 呼叫 <see cref="PlayFromStart"/> 起播。
///   showLastFrame=true → 直接定格在最後一幀、不播（重進場時旗標已成立、跪拜早已發生過的情況）。
///
/// 速度＝每實例 animFps；碰撞框/血量/可破壞由 MapLoader 以第一幀建立，與本元件無關（本元件只換顯示 sprite）。
/// </summary>
public class AnimatedMapObject : MonoBehaviour
{
    SpriteRenderer _sr;
    Sprite[] _frames;
    float _fps;
    int _idx;
    int _dir = 1;        // 乒乓方向（+1 正放 / -1 倒放）
    bool _pingPong;      // true = 乒乓（0→N-1→0 來回）
    bool _playOnce;      // true = 播一次到最後一幀就停
    float _timer;
    bool _ready;
    bool _playing;

    public void Initialize(SpriteRenderer sr, Sprite[] frames, float fps,
                           bool pingPong = false, bool playOnce = false,
                           bool startPlaying = true, bool showLastFrame = false)
    {
        _sr = sr;
        _frames = frames;
        _fps = fps > 0f ? fps : 8f;
        _pingPong = pingPong;
        _playOnce = playOnce;
        _idx = 0;
        _dir = 1;
        _timer = 0f;
        _ready = _sr != null && _frames != null && _frames.Length >= 2;

        if (_frames != null && _frames.Length > 0 && _sr != null)
        {
            if (showLastFrame)
            {
                // 直接定格在最後一幀、不播（跪拜早已發生過的重進場情況）。
                _idx = _frames.Length - 1;
                _sr.sprite = _frames[_idx];
                _playing = false;
            }
            else
            {
                _sr.sprite = _frames[0];
                _playing = startPlaying && _ready;
            }
        }
    }

    /// <summary>從第 0 幀重新起播（給旗標現身時「一出現就播」用；播一次的會播到最後一幀停住）。</summary>
    public void PlayFromStart()
    {
        _idx = 0;
        _dir = 1;
        _timer = 0f;
        if (_sr != null && _frames != null && _frames.Length > 0) _sr.sprite = _frames[0];
        _playing = _ready;
    }

    void Update()
    {
        if (!_playing || !_ready) return;
        float frameDur = 1f / _fps;
        _timer += Time.deltaTime;
        while (_timer >= frameDur)
        {
            _timer -= frameDur;
            if (Advance()) break;   // 播一次到底 → 停住
            _sr.sprite = _frames[_idx];
        }
    }

    // 推進一幀。回傳 true＝已到終點且該停（僅播一次模式）。
    // 循環＝0→N-1 繞回 0；乒乓＝0→N-1→0 來回；播一次＝0→N-1 到底停在最後一幀。
    bool Advance()
    {
        int n = _frames.Length;
        if (_playOnce)
        {
            if (_idx >= n - 1)
            {
                _idx = n - 1;
                _sr.sprite = _frames[_idx];
                _playing = false;   // 停在最後一幀
                return true;
            }
            _idx++;
            return false;
        }
        if (_pingPong)
        {
            _idx += _dir;
            if (_idx >= n - 1) { _idx = n - 1; _dir = -1; }
            else if (_idx <= 0) { _idx = 0; _dir = 1; }
        }
        else
        {
            _idx = (_idx + 1) % n;
        }
        return false;
    }
}
