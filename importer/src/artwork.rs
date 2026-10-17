//! LiveArea artwork: chosen SteamGridDB images by default, images built from the game's own intro sprites as
//! fallback (offline), or the player's own picks. Nothing copyrighted is stored in the repository.

use std::path::PathBuf;
use std::sync::OnceLock;
use std::time::Duration;

use image::imageops::{self, FilterType};
use image::{Rgba, RgbaImage};

#[derive(Clone, Copy, PartialEq, Eq)]
pub enum Slot {
    Bubble,
    Background,
    Startup,
}

impl Slot {
    pub const ALL: [Slot; 3] = [Slot::Bubble, Slot::Background, Slot::Startup];

    pub fn label(self) -> &'static str {
        match self {
            Slot::Bubble => "Bubble icon",
            Slot::Background => "LiveArea background",
            Slot::Startup => "Start button",
        }
    }

    pub fn size(self) -> (u32, u32) {
        match self {
            Slot::Bubble => (128, 128),
            Slot::Background => (840, 500),
            Slot::Startup => (280, 158),
        }
    }

    pub fn vpk_path(self) -> &'static str {
        match self {
            Slot::Bubble => "sce_sys/icon0.png",
            Slot::Background => "sce_sys/livearea/contents/bg.png",
            Slot::Startup => "sce_sys/livearea/contents/startup.png",
        }
    }

    /// Default image (SteamGridDB game 5439861): icon by Orion1189, hero by SeeDborg, logo by Orion1189.
    pub fn default_url(self) -> &'static str {
        match self {
            Slot::Bubble => "https://cdn2.steamgriddb.com/icon/92f0aaf782f117403757301fbdccb3ae.png",
            Slot::Background => "https://cdn2.steamgriddb.com/hero/de09c5e78507939780e178cbc9afe4b2.jpg",
            Slot::Startup => "https://cdn2.steamgriddb.com/logo/539c1f227ca587d2a328f2b5c9e75858.png",
        }
    }

    pub fn default_author(self) -> &'static str {
        match self {
            Slot::Background => "SeeDborg",
            Slot::Bubble | Slot::Startup => "Orion1189",
        }
    }

    // Transparent images (logos, icons) are placed on this colour; opaque ones fill the whole area.
    fn backdrop(self) -> Rgba<u8> {
        match self {
            Slot::Bubble => Rgba([212, 170, 52, 255]),
            Slot::Background => Rgba([0, 0, 0, 255]),
            Slot::Startup => Rgba([18, 24, 40, 255]),
        }
    }
}

#[derive(Clone, PartialEq, Eq)]
pub enum Source {
    Default,
    File(PathBuf),
    Url(String),
}

impl Source {
    pub fn describe(&self) -> String {
        match self {
            Source::Default => "Default (SteamGridDB)".into(),
            Source::File(p) => format!("File: {}", p.file_name().map_or_else(|| p.display().to_string(), |n| n.to_string_lossy().into())),
            Source::Url(u) => format!("Link: {}", u.rsplit('/').next().unwrap_or(u)),
        }
    }
}

#[derive(Clone)]
pub struct Choice {
    pub sources: [Source; 3],
    pub gate: GatePos,
    /// Horizontal position of the background crop (0 = left edge, 1 = right edge); None = automatic.
    pub bg_focus: Option<f32>,
}

impl Default for Choice {
    fn default() -> Self {
        Choice { sources: [Source::Default, Source::Default, Source::Default], gate: GatePos::Right, bg_focus: None }
    }
}

impl Choice {
    pub fn get(&self, slot: Slot) -> &Source {
        &self.sources[slot as usize]
    }

    pub fn set(&mut self, slot: Slot, source: Source) {
        self.sources[slot as usize] = source;
    }

    pub fn focus(&self) -> f32 {
        self.bg_focus.unwrap_or_else(|| auto_focus(self.get(Slot::Background), self.gate))
    }
}

/// Where the LiveArea shows the start button. The Vita only offers these through the template styles.
#[derive(Clone, Copy, PartialEq, Eq)]
pub enum GatePos {
    Center,
    Right,
}

impl GatePos {
    pub const ALL: [GatePos; 2] = [GatePos::Center, GatePos::Right];

    pub fn label(self) -> &'static str {
        match self {
            GatePos::Center => "Centre",
            GatePos::Right => "Right",
        }
    }

    fn style(self) -> &'static str {
        match self {
            GatePos::Center => "a1",
            GatePos::Right => "a5",
        }
    }

    /// Start button (280x158) in background coordinates; the 840x500 background sits at (60, 44) on screen.
    pub fn rect(self) -> (f32, f32, f32, f32) {
        match self {
            GatePos::Center => (280.0, 139.0, 280.0, 158.0),
            GatePos::Right => (520.0, 105.0, 280.0, 158.0),
        }
    }

    // Where the egg should end up in the background so the button doesn't cover it.
    fn egg_target(self) -> f32 {
        match self {
            GatePos::Center => 700.0,
            GatePos::Right => 260.0,
        }
    }
}

pub fn template_xml(gate: GatePos) -> String {
    format!(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<livearea style=\"{}\" format-ver=\"01.00\" content-rev=\"2\">\n  \
         <livearea-background>\n    <image>bg.png</image>\n  </livearea-background>\n  \
         <gate>\n    <startup-image>startup.png</startup-image>\n  </gate>\n</livearea>\n",
        gate.style()
    )
}

// Egg position in the (mirrored) default hero image (fraction of its width) and in the fallback panorama (pixels).
const HERO_EGG_X: f32 = 0.35;
const PANO_EGG_X: f32 = 1194.0;

/// Background crop position that keeps the egg clear of the start button; centred for the player's own images.
pub fn auto_focus(source: &Source, gate: GatePos) -> f32 {
    if *source != Source::Default {
        return 0.5;
    }
    // The 1920x620 hero is scaled to 1548x500, leaving 708 px to crop.
    ((HERO_EGG_X * 1548.0 - gate.egg_target()) / 708.0).clamp(0.0, 1.0)
}

pub fn fallback_focus(gate: GatePos) -> f32 {
    ((PANO_EGG_X - gate.egg_target()) / 1560.0).clamp(0.0, 1.0)
}

fn agent() -> &'static ureq::Agent {
    static AGENT: OnceLock<ureq::Agent> = OnceLock::new();
    AGENT.get_or_init(|| {
        ureq::Agent::config_builder()
            .timeout_connect(Some(Duration::from_secs(5)))
            .timeout_global(Some(Duration::from_secs(30)))
            .build()
            .into()
    })
}

/// HTTPS GET; downloaded images are cached so repeated builds work offline.
fn fetch(url: &str) -> Result<Vec<u8>, String> {
    if !url.starts_with("https://") {
        return Err("only https links are supported".into());
    }
    let cache = cache_path(url);
    if let Some(data) = cache.as_ref().and_then(|p| std::fs::read(p).ok()) {
        return Ok(data);
    }
    let data = agent()
        .get(url)
        .call()
        .and_then(|mut r| r.body_mut().with_config().limit(32 << 20).read_to_vec())
        .map_err(|e| e.to_string())?;
    if let Some(p) = cache {
        let _ = std::fs::create_dir_all(p.parent().unwrap());
        let _ = std::fs::write(p, &data);
    }
    Ok(data)
}

fn cache_path(url: &str) -> Option<PathBuf> {
    let name: String = url.rsplit('/').next()?.chars().filter(|c| c.is_ascii_alphanumeric() || matches!(c, '.' | '-' | '_')).collect();
    let kind = url.split('/').rev().nth(1)?;
    (!name.is_empty()).then(|| dirs::cache_dir().map(|d| d.join("ladxhd-vita-importer").join(format!("{kind}-{name}"))))?
}

pub fn decode(data: &[u8]) -> Result<RgbaImage, String> {
    let mut reader = image::ImageReader::new(std::io::Cursor::new(data)).with_guessed_format().map_err(|e| e.to_string())?;
    let mut limits = image::Limits::default();
    limits.max_image_width = Some(8192);
    limits.max_image_height = Some(8192);
    reader.limits(limits);
    Ok(reader.decode().map_err(|e| e.to_string())?.to_rgba8())
}

pub fn load(slot: Slot, source: &Source) -> Result<RgbaImage, String> {
    let data = match source {
        Source::Default => fetch(slot.default_url())?,
        Source::Url(u) => fetch(u)?,
        Source::File(p) => std::fs::read(p).map_err(|e| format!("{}: {e}", p.display()))?,
    };
    let img = decode(&data)?;
    // Mirrored so the egg is on the left, clear of the start button on the right.
    Ok(if slot == Slot::Background && *source == Source::Default { imageops::flip_horizontal(&img) } else { img })
}

fn is_transparent(img: &RgbaImage) -> bool {
    img.pixels().any(|p| p[3] < 250)
}

fn cover_scale(slot: Slot, src: &RgbaImage) -> f32 {
    let (w, h) = slot.size();
    (w as f32 / src.width() as f32).max(h as f32 / src.height() as f32)
}

fn resized(src: &RgbaImage, scale: f32) -> RgbaImage {
    let (w, h) = (((src.width() as f32 * scale).round() as u32).max(1), ((src.height() as f32 * scale).round() as u32).max(1));
    imageops::resize(src, w, h, FilterType::Lanczos3)
}

/// Scales any picture to the slot: transparent ones are fitted onto the backdrop, opaque ones fill the slot and
/// are cropped, `focus` choosing the part that stays (0 = left/top, 1 = right/bottom).
pub fn compose(slot: Slot, src: &RgbaImage, focus: f32) -> RgbaImage {
    let (w, h) = slot.size();
    let mut out = RgbaImage::from_pixel(w, h, slot.backdrop());
    if is_transparent(src) {
        // 8% margin keeps logos clear of the bubble's round mask and the gate frame.
        let scaled = resized(src, (w as f32 * 0.92 / src.width() as f32).min(h as f32 * 0.92 / src.height() as f32));
        imageops::overlay(&mut out, &scaled, (w as i64 - scaled.width() as i64) / 2, (h as i64 - scaled.height() as i64) / 2);
    } else {
        let scaled = resized(src, cover_scale(slot, src));
        let x = ((scaled.width().saturating_sub(w)) as f32 * focus).round() as i64;
        let y = ((scaled.height().saturating_sub(h)) as f32 * focus).round() as i64;
        imageops::overlay(&mut out, &scaled, -x, -y);
    }
    out
}

/// Picture for the importer window. For an opaque background it returns the whole picture at half the final
/// size (`true`), so the window can show any crop position without recomputing.
pub fn preview(slot: Slot, source: &Source) -> Result<(RgbaImage, bool), String> {
    let src = load(slot, source)?;
    if slot == Slot::Background && !is_transparent(&src) {
        Ok((resized(&src, cover_scale(slot, &src) * 0.5), true))
    } else {
        Ok((compose(slot, &src, 0.5), false))
    }
}

fn sprite(sheet: &RgbaImage, atlas: &str, name: &str) -> Result<RgbaImage, String> {
    let line = atlas.lines().find_map(|l| l.strip_prefix(name)?.strip_prefix(':')).ok_or(format!("{name} missing in intro.atlas"))?;
    let v: Vec<u32> = line.split(',').take(4).filter_map(|n| n.trim().parse().ok()).collect();
    match v[..] {
        [x, y, w, h] if x + w <= sheet.width() && y + h <= sheet.height() => Ok(imageops::crop_imm(sheet, x, y, w, h).to_image()),
        _ => Err(format!("{name}: bad atlas entry")),
    }
}

fn pixel_scale(img: &RgbaImage, n: u32) -> RgbaImage {
    imageops::resize(img, img.width() * n, img.height() * n, FilterType::Nearest)
}

/// Mirrored mountains | egg scene | mountains at 3x, 500 px high; the mountain sprite's empty sky takes the egg
/// scene's edge colour.
fn panorama(sheet: &RgbaImage, atlas: &str, sky: &RgbaImage) -> Result<RgbaImage, String> {
    let mut hills = sprite(sheet, atlas, "intro_mountain")?;
    for (_, y, p) in hills.enumerate_pixels_mut() {
        if p[3] == 0 {
            *p = *sky.get_pixel(sky.width() - 1, y.min(sky.height() - 1));
        }
    }
    let mut pano = RgbaImage::new(hills.width() * 2 + sky.width(), sky.height());
    imageops::overlay(&mut pano, &imageops::flip_horizontal(&hills), 0, 0);
    imageops::overlay(&mut pano, sky, hills.width() as i64, 0);
    imageops::overlay(&mut pano, &hills, (hills.width() + sky.width()) as i64, 0);
    let big = pixel_scale(&pano, 3);
    Ok(imageops::crop_imm(&big, 0, 100, big.width(), 500).to_image())
}

/// Fallback artwork for the importer window, in the same form as `preview`.
pub fn fallback_preview(slot: Slot, intro_png: &[u8], intro_atlas: &[u8]) -> Result<(RgbaImage, bool), String> {
    if slot == Slot::Background {
        let sheet = decode(intro_png)?;
        let atlas = String::from_utf8_lossy(intro_atlas);
        let sky = sprite(&sheet, &atlas, "intro_background")?;
        Ok((resized(&panorama(&sheet, &atlas, &sky)?, 0.5), true))
    } else {
        Ok((fallback(slot, intro_png, intro_atlas, 0.5)?, false))
    }
}

/// Fallback artwork from the game's intro sprites (Data/Intro/intro.png of the player's copy).
pub fn fallback(slot: Slot, intro_png: &[u8], intro_atlas: &[u8], focus: f32) -> Result<RgbaImage, String> {
    let sheet = decode(intro_png)?;
    let atlas = String::from_utf8_lossy(intro_atlas);
    let sky = sprite(&sheet, &atlas, "intro_background")?;
    Ok(match slot {
        // The egg on the mountain top.
        Slot::Bubble => pixel_scale(&imageops::crop_imm(&sky, 46, 48, 64, 64).to_image(), 2),
        Slot::Background => {
            let pano = panorama(&sheet, &atlas, &sky)?;
            let x = ((pano.width() - 840) as f32 * focus).round() as u32;
            imageops::crop_imm(&pano, x, 0, 840, 500).to_image()
        }
        Slot::Startup => {
            let mut logo = sprite(&sheet, &atlas, "intro_logo_0")?;
            imageops::overlay(&mut logo, &sprite(&sheet, &atlas, "intro_logo_1")?, 0, 0);
            let dx = sprite(&sheet, &atlas, "intro_dx")?;
            let mut out = RgbaImage::from_pixel(280, 158, *sky.get_pixel(0, 0));
            imageops::overlay(&mut out, &pixel_scale(&logo, 2), 14, 4);
            imageops::overlay(&mut out, &dx, (280 - dx.width() - 10) as i64, 116);
            out
        }
    })
}

/// The Vita shell needs 8-bit palette PNGs.
pub fn encode_indexed(img: &RgbaImage) -> Result<Vec<u8>, String> {
    let rgb: Vec<[u8; 3]> = img.pixels().map(|p| [p[0], p[1], p[2]]).collect();
    let mut palette: Vec<[u8; 3]> = rgb.clone();
    palette.sort_unstable();
    palette.dedup();
    let indices: Vec<u8> = if palette.len() <= 256 {
        rgb.iter().map(|c| palette.binary_search(c).unwrap() as u8).collect()
    } else {
        let rgba: Vec<u8> = rgb.iter().flat_map(|c| [c[0], c[1], c[2], 255]).collect();
        let nq = color_quant::NeuQuant::new(10, 256, &rgba);
        palette = nq.color_map_rgb().chunks(3).map(|c| [c[0], c[1], c[2]]).collect();
        rgba.chunks(4).map(|p| nq.index_of(p) as u8).collect()
    };
    let mut out = Vec::new();
    let mut enc = png::Encoder::new(&mut out, img.width(), img.height());
    enc.set_color(png::ColorType::Indexed);
    enc.set_depth(png::BitDepth::Eight);
    enc.set_palette(palette.concat());
    let mut writer = enc.write_header().map_err(|e| e.to_string())?;
    writer.write_image_data(&indices).map_err(|e| e.to_string())?;
    writer.finish().map_err(|e| e.to_string())?;
    Ok(out)
}

/// Renders all slots and the LiveArea template for the VPK. The default falls back to the game's own sprites
/// when SteamGridDB is unreachable.
pub fn build_all(choice: &Choice, intro_png: &[u8], intro_atlas: &[u8], log: &mut dyn FnMut(String)) -> Result<Vec<(String, Vec<u8>)>, String> {
    let mut offline = false;
    let mut out = Vec::new();
    for slot in Slot::ALL {
        let source = choice.get(slot);
        let focus = |s: &Source| choice.bg_focus.unwrap_or_else(|| auto_focus(s, choice.gate));
        let picked = if offline && *source == Source::Default { Err("offline".to_string()) } else { load(slot, source) };
        let img = match picked {
            Ok(img) => compose(slot, &img, focus(source)),
            Err(e) => {
                if *source != Source::Default {
                    log(format!("{}: {e}; using the default", slot.label()));
                }
                match (source != &Source::Default && !offline).then(|| load(slot, &Source::Default)) {
                    Some(Ok(img)) => compose(slot, &img, focus(&Source::Default)),
                    _ => {
                        offline = true;
                        log(format!("{}: SteamGridDB not reachable, using the game's own artwork", slot.label()));
                        fallback(slot, intro_png, intro_atlas, choice.bg_focus.unwrap_or_else(|| fallback_focus(choice.gate)))?
                    }
                }
            }
        };
        out.push((slot.vpk_path().to_string(), encode_indexed(&img)?));
    }
    out.push(("sce_sys/livearea/contents/template.xml".into(), template_xml(choice.gate).into_bytes()));
    Ok(out)
}
