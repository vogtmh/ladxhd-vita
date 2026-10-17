//! Startup loading animation for the Vita (ui/spinner.bin), made from the game's own save icon.

use std::f32::consts::TAU;
use std::io::Cursor;

// The game's UI scale on the Vita screen (960x544); the eboot draws the frames 1:1.
const SCALE: usize = 4;
const FRAMES: usize = 30;
const FRAME_MS: u16 = 40;
// UiRectangle.OpaqueHudColor, which the game uses for the save icon when UI blur is off (always on the Vita).
const HUD_COLOR: [f32; 4] = [230.0, 230.0, 170.0, 0.9];

fn atlas_rect(atlas: &str, name: &str) -> Option<[usize; 4]> {
    let line = atlas.lines().find_map(|l| l.strip_prefix(name)?.strip_prefix(':'))?;
    let v: Vec<usize> = line.split(',').take(4).map(|n| n.trim().parse().ok()).collect::<Option<_>>()?;
    (v.len() == 4).then(|| [v[0], v[1], v[2], v[3]])
}

/// "SPN1", u16 width, height, frame count, frame ms, then premultiplied RGBA frames.
pub fn make(ui_png: &[u8], ui_atlas: &[u8]) -> Result<Vec<u8>, String> {
    let [sx, sy, sw, sh] = atlas_rect(&String::from_utf8_lossy(ui_atlas), "save_icon").ok_or("save_icon missing in ui.atlas")?;
    let mut decoder = png::Decoder::new(Cursor::new(ui_png));
    decoder.set_transformations(png::Transformations::normalize_to_color8() | png::Transformations::ALPHA);
    let mut reader = decoder.read_info().map_err(|e| format!("ui.png: {e}"))?;
    let mut pixels = vec![0; reader.output_buffer_size().ok_or("ui.png: too large")?];
    let info = reader.next_frame(&mut pixels).map_err(|e| format!("ui.png: {e}"))?;
    if info.color_type != png::ColorType::Rgba || sx + sw > info.width as usize || sy + sh > info.height as usize {
        return Err("ui.png: unexpected format".into());
    }

    let (w, h) = (sw * SCALE, sh * SCALE);
    let stride = (w + 7) & !7; // GXM linear textures need a width that is a multiple of 8
    let mut out = b"SPN1".to_vec();
    for v in [stride as u16, h as u16, FRAMES as u16, FRAME_MS] {
        out.extend_from_slice(&v.to_le_bytes());
    }
    for f in 0..FRAMES {
        let pulse = 0.35 + 0.65 * (0.5 - 0.5 * (TAU * f as f32 / FRAMES as f32).cos());
        for y in 0..h {
            for x in 0..stride {
                let p = if x < w { &pixels[((sy + y / SCALE) * info.line_size) + (sx + x / SCALE) * 4..][..4] } else { &[0; 4][..] };
                let a = p[3] as f32 / 255.0 * HUD_COLOR[3] * pulse;
                for c in 0..3 {
                    out.push((HUD_COLOR[c] * p[c] as f32 / 255.0 * a).round() as u8);
                }
                out.push((a * 255.0).round() as u8);
            }
        }
    }
    Ok(out)
}
