#!/usr/bin/env python3
"""
把 tools/ui/speech_bubble/*.svg 轉成 DipanProj_Main/Assets/Resources/UI/SpeechBubble/*.png（頭上對話框底板）。
需要：pip install cairosvg
用法：python3 tools/ui/speech_bubble/build_speech_bubble.py
九宮格邊界寫在 MonsterSpeechPanel.cs 的 Styles 表裡（不靠 .meta），改了 SVG 的圓角大小要同步改那邊。
"""
import glob, os, cairosvg
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', '..', 'DipanProj_Main', 'Assets', 'Resources', 'UI', 'SpeechBubble')
os.makedirs(OUT, exist_ok=True)
for svg in sorted(glob.glob(os.path.join(HERE, '*.svg'))):
    png = os.path.join(OUT, os.path.splitext(os.path.basename(svg))[0] + '.png')
    cairosvg.svg2png(url=svg, write_to=png)
    print('→', os.path.relpath(png, os.path.join(HERE, '..', '..', '..')))
