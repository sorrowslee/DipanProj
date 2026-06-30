using UnityEngine;

/// <summary>
/// 動畫地上物的播放元件：用一個 SpriteRenderer 原地循環播放幀序列（永不自毀）。
/// 由 <see cref="MapLoader"/> 在建立動畫地上物時掛上並 Initialize。
///
/// 與 VfxInstance 不同：這是「常駐循環」（地上物一直在場上），不靠壽命計時、不自毀；
/// 速度由每個放置實例的 animFps 決定（.dipanmap 的 objects[].animFps，編輯器內每實例可調）。
/// 碰撞框 / 血量 / 可破壞由 MapLoader 以第一幀建立，與本元件無關（本元件只換顯示用的 sprite）。
/// </summary>
public class AnimatedMapObject : MonoBehaviour
{
    SpriteRenderer _sr;
    Sprite[] _frames;
    float _fps;
    int _idx;
    int _dir = 1;        // 乒乓方向（+1 正放 / -1 倒放）
    bool _pingPong;      // true = 乒乓（0→N-1→0 來回；AI 產的圖首尾接不順時用此模式，接縫消失）
    float _timer;
    bool _ready;

    public void Initialize(SpriteRenderer sr, Sprite[] frames, float fps, bool pingPong = false)
    {
        _sr = sr;
        _frames = frames;
        _fps = fps > 0f ? fps : 8f;
        _pingPong = pingPong;
        _idx = 0;
        _dir = 1;
        _timer = 0f;
        _ready = _sr != null && _frames != null && _frames.Length >= 2;
        if (_sr != null && _frames != null && _frames.Length > 0)
            _sr.sprite = _frames[0];
    }

    void Update()
    {
        if (!_ready) return;
        float frameDur = 1f / _fps;
        _timer += Time.deltaTime;
        while (_timer >= frameDur)
        {
            _timer -= frameDur;
            Advance();
            _sr.sprite = _frames[_idx];
        }
    }

    // 推進一幀：循環 = 0→N-1 繞回 0；乒乓 = 0→N-1→0 來回（端點各停一幀，接縫不跳變）。
    void Advance()
    {
        int n = _frames.Length;
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
    }
}
