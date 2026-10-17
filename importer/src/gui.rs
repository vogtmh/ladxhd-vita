use std::path::{Path, PathBuf};
use std::sync::mpsc::{self, Receiver};
use std::sync::{Arc, Mutex};
use std::thread::JoinHandle;
use std::time::Duration;

use eframe::egui;
use image::RgbaImage;

use crate::artwork::{self, Choice, GatePos, Slot, Source};
use crate::vpk;

type Outcome = Result<Vec<String>, String>;
/// Image, uncropped background, game's own artwork, note.
type Shown<I> = (I, bool, bool, Option<String>);
type ImageRx = Receiver<Result<Shown<egui::ColorImage>, String>>;
type Intro = Arc<(Vec<u8>, Vec<u8>)>;

struct Job {
    status: Arc<Mutex<(String, f32)>>,
    handle: JoinHandle<Outcome>,
    output: PathBuf,
}

/// Loads a picture on a worker thread so the window stays responsive.
fn spawn_image(ctx: &egui::Context, load: impl FnOnce() -> Result<Shown<RgbaImage>, String> + Send + 'static) -> ImageRx {
    let (tx, rx) = mpsc::channel();
    let ctx = ctx.clone();
    std::thread::spawn(move || {
        let img = load().map(|(i, cover, own, note)| {
            (egui::ColorImage::from_rgba_unmultiplied([i.width() as usize, i.height() as usize], i.as_raw()), cover, own, note)
        });
        let _ = tx.send(img);
        ctx.request_repaint();
    });
    rx
}

/// What the VPK will get: the chosen picture, else the default, else the game's own artwork.
fn load_preview(slot: Slot, source: &Source, intro: Option<Intro>) -> Result<Shown<RgbaImage>, String> {
    let err = match artwork::preview(slot, source) {
        Ok((img, cover)) => return Ok((img, cover, false, None)),
        Err(e) => e,
    };
    let custom = *source != Source::Default;
    if custom {
        if let Ok((img, cover)) = artwork::preview(slot, &Source::Default) {
            return Ok((img, cover, false, Some(format!("{err} — the default will be used"))));
        }
    }
    match intro {
        Some(i) => artwork::fallback_preview(slot, &i.0, &i.1).map(|(img, cover)| (img, cover, true, custom.then_some(err))),
        None => Err(format!("{err} — select the game folder to see the game's own artwork")),
    }
}

/// A picture that is loading, loaded or failed.
#[derive(Default)]
struct Picture {
    rx: Option<ImageRx>,
    tex: Option<egui::TextureHandle>,
    cover: bool,
    own: bool,
    note: Option<String>,
    error: Option<String>,
}

impl Picture {
    fn loading(rx: ImageRx) -> Self {
        Picture { rx: Some(rx), ..Default::default() }
    }

    fn poll(&mut self, ctx: &egui::Context, name: &str) {
        if let Some(r) = self.rx.as_ref().and_then(|rx| rx.try_recv().ok()) {
            self.rx = None;
            match r {
                Ok((img, cover, own, note)) => {
                    self.tex = Some(ctx.load_texture(name, img, egui::TextureOptions::LINEAR));
                    (self.cover, self.own, self.note) = (cover, own, note);
                }
                Err(e) => self.error = Some(e),
            }
        }
    }

    fn paint(&self, ui: &egui::Ui, rect: egui::Rect, uv: egui::Rect, radius: f32) {
        if let Some(tex) = &self.tex {
            egui::Image::new(tex).uv(uv).corner_radius(radius).paint_at(ui, rect);
        } else {
            let text = if self.rx.is_some() { "Loading…" } else { "Not available" };
            let p = ui.painter();
            p.rect_filled(rect, radius, ui.visuals().extreme_bg_color);
            p.rect_stroke(rect, radius, ui.visuals().widgets.noninteractive.bg_stroke, egui::StrokeKind::Inside);
            p.text(rect.center(), egui::Align2::CENTER_CENTER, text, egui::FontId::proportional(11.0), ui.visuals().weak_text_color());
        }
    }

    /// The part of the background the Vita shows at this crop position.
    fn crop_uv(&self, focus: f32) -> egui::Rect {
        match &self.tex {
            Some(tex) if self.cover => {
                // The preview is half the final size, so the visible part is 420x250 texels.
                let [tw, th] = tex.size();
                let (cw, ch) = ((420.0 / tw as f32).min(1.0), (250.0 / th as f32).min(1.0));
                egui::Rect::from_min_size(egui::pos2((1.0 - cw) * focus, (1.0 - ch) * focus), egui::vec2(cw, ch))
            }
            _ => FULL_UV,
        }
    }
}

const FULL_UV: egui::Rect = egui::Rect::from_min_max(egui::pos2(0.0, 0.0), egui::pos2(1.0, 1.0));

#[derive(Default)]
struct App {
    folder: String,
    job: Option<Job>,
    result: Option<Result<(PathBuf, Vec<String>), String>>,
    art: Choice,
    previews: [Picture; 3],
    previews_started: bool,
    intro: Option<Intro>,
    intro_rx: Option<Receiver<Result<Intro, String>>>,
    intro_root: Option<PathBuf>,
    /// Right edge of the artwork boxes, so the folder row can end there too.
    content_right: Option<f32>,
}

impl App {
    fn start(&mut self, input: PathBuf, output: PathBuf) {
        let status = Arc::new(Mutex::new(("Starting".to_string(), 0.0)));
        let shared = status.clone();
        let (out, art) = (output.clone(), self.art.clone());
        let handle = std::thread::spawn(move || {
            vpk::build(&input, Some(&out), None, &art, &mut |step: &str, done| {
                *shared.lock().unwrap() = (step.to_string(), done);
            })
        });
        self.result = None;
        self.job = Some(Job { status, handle, output });
    }

    fn set_art(&mut self, ctx: &egui::Context, slot: Slot, source: Source) {
        self.art.set(slot, source.clone());
        let intro = self.intro.clone();
        self.previews[slot as usize] = Picture::loading(spawn_image(ctx, move || load_preview(slot, &source, intro)));
    }

    /// Reads the intro graphics of the selected game folder for the offline artwork previews.
    fn update_intro(&mut self, ctx: &egui::Context, root: Option<PathBuf>) {
        if root != self.intro_root {
            self.intro_root = root.clone();
            self.intro = None;
            self.intro_rx = root.map(|r| {
                let (tx, rx) = mpsc::channel();
                let ctx = ctx.clone();
                std::thread::spawn(move || {
                    let _ = tx.send(vpk::intro_files(&r).map(Arc::new));
                    ctx.request_repaint();
                });
                rx
            });
        }
        // Errors are left to the import, which explains them.
        if let Some(Ok(intro)) = self.intro_rx.as_ref().and_then(|rx| rx.try_recv().ok()) {
            self.intro_rx = None;
            self.intro = Some(intro);
            for slot in Slot::ALL {
                let p = &self.previews[slot as usize];
                if p.error.is_some() || p.rx.is_some() {
                    self.set_art(ctx, slot, self.art.get(slot).clone());
                }
            }
        }
    }

    fn bg_focus(&self) -> f32 {
        match self.art.bg_focus {
            Some(f) => f,
            None if self.previews[Slot::Background as usize].own => artwork::fallback_focus(self.art.gate),
            None => self.art.focus(),
        }
    }

    fn main_panel(&mut self, ui: &mut egui::Ui, ctx: &egui::Context) {
        ui.heading("Link's Awakening DX HD for PS Vita");
        ui.label(format!("Creates an installable VPK (game version {}) from your copy of the game.", vpk::GAME_VERSION));
        ui.add_space(12.0);

        let busy = self.job.is_some();
        ui.label("Game folder: the original v1.0.0 release, or a copy patched to v1.1.4 or newer.");
        let row_w = self.content_right.map_or(ui.available_width(), |r| r - ui.cursor().left());
        ui.allocate_ui_with_layout(egui::vec2(row_w, 20.0), egui::Layout::right_to_left(egui::Align::Center), |ui| {
            if ui.add_enabled(!busy, egui::Button::new("Browse…")).clicked() {
                if let Some(dir) = rfd::FileDialog::new().pick_folder() {
                    self.folder = dir.display().to_string();
                    self.result = None;
                }
            }
            ui.add_enabled(!busy, egui::TextEdit::singleline(&mut self.folder).desired_width(ui.available_width()));
        });
        let folder = self.folder.trim();
        let root = (!folder.is_empty()).then(|| vpk::find_game_root(Path::new(folder))).flatten();
        if !folder.is_empty() && root.is_none() {
            ui.colored_label(ui.visuals().error_fg_color, "This folder doesn't contain the game's Content and Data folders.");
        }
        self.update_intro(ctx, root.clone());
        ui.add_space(12.0);

        ui.add_enabled_ui(!busy, |ui| {
            if self.artwork_section(ui, ctx, root.is_some()) {
                if let Some(output) = rfd::FileDialog::new().set_file_name(vpk::default_file_name()).add_filter("VPK", &["vpk"]).save_file() {
                    self.start(root.clone().unwrap(), output);
                }
            }
        });
    }

    /// Progress and outcome of the import, kept below the scrolling content so it is always visible.
    fn status_panel(&mut self, ui: &mut egui::Ui, ctx: &egui::Context) {
        if let Some(job) = &self.job {
            let (step, done) = job.status.lock().unwrap().clone();
            ui.add(egui::ProgressBar::new(done).text(step));
            ctx.request_repaint_after(Duration::from_millis(50));
            if job.handle.is_finished() {
                let job = self.job.take().unwrap();
                let result = job.handle.join().unwrap_or_else(|_| Err("The importer crashed.".into()));
                self.result = Some(result.map(|notes| (job.output, notes)));
            }
        }

        match &self.result {
            Some(Ok((path, notes))) => {
                ui.label(format!("Created {}", path.display()));
                ui.label("Copy it to the Vita and install it with VitaShell.");
                for n in notes {
                    ui.colored_label(ui.visuals().warn_fg_color, n);
                }
            }
            Some(Err(e)) => {
                ui.colored_label(ui.visuals().error_fg_color, e);
            }
            None => {}
        }
    }

    /// Returns true when Create VPK was clicked.
    fn artwork_section(&mut self, ui: &mut egui::Ui, ctx: &egui::Context, can_create: bool) -> bool {
        if !self.previews_started {
            self.previews_started = true;
            for slot in Slot::ALL {
                self.set_art(ctx, slot, self.art.get(slot).clone());
            }
        }
        ui.strong("Home screen artwork");
        ui.label("By default, chosen SteamGridDB images are used; without internet, artwork from the game's own files.");
        ui.add_space(10.0);
        for slot in Slot::ALL {
            self.previews[slot as usize].poll(ctx, slot.label());
        }

        let mut anchors = [egui::Pos2::ZERO; 3];
        let mut groups = [egui::Rect::NOTHING; 3];
        let mut create = false;
        ui.horizontal_top(|ui| {
            anchors = self.mockup(ui);
            ui.add_space(36.0);
            ui.vertical(|ui| {
                for slot in Slot::ALL {
                    groups[slot as usize] = ui.group(|ui| {
                        ui.set_width(250.0);
                        self.slot_controls(ui, ctx, slot);
                    }).response.rect;
                    ui.add_space(4.0);
                }
                ui.add_space(6.0);
                let button = egui::Button::new(egui::RichText::new("Create VPK…").size(17.0).strong())
                    .min_size(egui::vec2(groups[0].width(), 40.0));
                create = ui.add_enabled(can_create, button).on_disabled_hover_text("Select the game folder first.").clicked();
            });
        });

        let accent = ui.visuals().selection.bg_fill;
        let p = ui.painter();
        for slot in Slot::ALL {
            let (a, g) = (anchors[slot as usize], groups[slot as usize]);
            let b = egui::pos2(g.left(), g.top() + 16.0);
            p.line_segment([a, b], egui::Stroke::new(3.5, egui::Color32::from_black_alpha(70)));
            p.line_segment([a, b], egui::Stroke::new(1.5, accent));
            p.circle(a, 4.0, accent, egui::Stroke::new(1.5, egui::Color32::WHITE));
            p.circle_filled(b, 3.0, accent);
        }
        self.content_right = Some(groups[0].right());
        create
    }

    /// The Vita home screen: an animated bubble and the LiveArea card with its start button.
    /// Returns where the connector lines start for the bubble, background and start button.
    fn mockup(&self, ui: &mut egui::Ui) -> [egui::Pos2; 3] {
        const CARD_W: f32 = 380.0;
        const BUBBLE_D: f32 = 76.0;
        let s = CARD_W / 840.0;
        let card_size = egui::vec2(CARD_W, 500.0 * s);
        let (area, _) = ui.allocate_exact_size(egui::vec2(CARD_W, BUBBLE_D + 24.0 + card_size.y), egui::Sense::hover());

        // Vita bubbles float gently.
        let t = ui.input(|i| i.time) as f32;
        let d = BUBBLE_D * (1.0 + 0.025 * (t * 1.7 + 1.0).sin());
        let c = egui::pos2(area.center().x, area.top() + BUBBLE_D / 2.0 + 6.0 + (t * 1.7).sin() * 3.0);
        let p = ui.painter();
        p.circle_filled(c + egui::vec2(0.0, 5.0), d / 2.0, egui::Color32::from_black_alpha(50));
        self.previews[Slot::Bubble as usize].paint(ui, egui::Rect::from_center_size(c, egui::vec2(d, d)), FULL_UV, d / 2.0);
        p.circle_stroke(c, d / 2.0, egui::Stroke::new(2.0, egui::Color32::from_white_alpha(180)));
        ui.ctx().request_repaint_after(Duration::from_millis(16));

        let card = egui::Rect::from_min_size(egui::pos2(area.left(), area.bottom() - card_size.y), card_size);
        let bg = &self.previews[Slot::Background as usize];
        bg.paint(ui, card, bg.crop_uv(self.bg_focus()), 10.0);
        let (x, y, w, h) = self.art.gate.rect();
        let gate = egui::Rect::from_min_size(card.min + egui::vec2(x * s, y * s), egui::vec2(w * s, h * s));
        self.previews[Slot::Startup as usize].paint(ui, gate, FULL_UV, 6.0);
        p.rect_stroke(gate, 6.0, egui::Stroke::new(1.5, egui::Color32::WHITE), egui::StrokeKind::Outside);

        [c + egui::vec2(d / 2.0, 0.0), egui::pos2(card.right() - 14.0, card.top() + 18.0), egui::pos2(gate.right(), gate.center().y)]
    }

    fn slot_controls(&mut self, ui: &mut egui::Ui, ctx: &egui::Context, slot: Slot) {
        let (w, h) = slot.size();
        ui.strong(format!("{} ({w}×{h})", slot.label()));
        let pic = &self.previews[slot as usize];
        if pic.own {
            ui.label("The game's own artwork (SteamGridDB not reachable)");
        } else {
            ui.label(self.art.get(slot).describe());
            if *self.art.get(slot) == Source::Default {
                ui.hyperlink_to(format!("Original by {}", slot.default_author()), slot.default_url());
            }
        }
        if let Some(n) = pic.note.as_ref().or(pic.error.as_ref()) {
            ui.colored_label(ui.visuals().warn_fg_color, n);
        }
        ui.horizontal(|ui| {
            if ui.button("Image file…").clicked() {
                if let Some(p) = rfd::FileDialog::new().add_filter("Images", &["png", "jpg", "jpeg", "webp"]).pick_file() {
                    self.set_art(ctx, slot, Source::File(p));
                }
            }
            if ui.add_enabled(*self.art.get(slot) != Source::Default, egui::Button::new("Default")).clicked() {
                self.set_art(ctx, slot, Source::Default);
            }
        });
        match slot {
            Slot::Background => {
                ui.horizontal(|ui| {
                    ui.label("Position:");
                    let mut auto = self.art.bg_focus.is_none();
                    if ui.checkbox(&mut auto, "Automatic").changed() {
                        self.art.bg_focus = if auto { None } else { Some(self.bg_focus()) };
                    }
                    let mut focus = self.bg_focus();
                    if ui.add_enabled(!auto, egui::Slider::new(&mut focus, 0.0..=1.0).show_value(false)).changed() {
                        self.art.bg_focus = Some(focus);
                    }
                });
            }
            Slot::Startup => {
                ui.horizontal(|ui| {
                    ui.label("Place:");
                    for g in GatePos::ALL {
                        ui.radio_value(&mut self.art.gate, g, g.label());
                    }
                });
            }
            Slot::Bubble => {}
        }
    }
}

impl eframe::App for App {
    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        let ctx = ui.ctx().clone();
        if self.job.is_some() || self.result.is_some() {
            egui::Panel::bottom("status").show(ui, |ui| {
                ui.add_space(6.0);
                self.status_panel(ui, &ctx);
                ui.add_space(4.0);
            });
        }
        egui::CentralPanel::default().show(ui, |ui| {
            egui::ScrollArea::vertical().auto_shrink(false).show(ui, |ui| self.main_panel(ui, &ctx));
        });
    }
}

pub fn run() -> Result<(), String> {
    #[allow(unused_mut)]
    let mut options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default().with_inner_size([712.0, 600.0]).with_min_inner_size([520.0, 300.0]),
        ..Default::default()
    };
    // Probing the Vulkan/OpenGL drivers crashed on a Windows 10 PC; DirectX 12 is always there. WGPU_BACKEND still overrides.
    #[cfg(windows)]
    if std::env::var_os("WGPU_BACKEND").is_none() {
        if let eframe::egui_wgpu::WgpuSetup::CreateNew(setup) = &mut options.wgpu_options.wgpu_setup {
            setup.instance_descriptor.backends = eframe::wgpu::Backends::DX12;
        }
    }
    eframe::run_native("LADXHD Vita Importer", options, Box::new(|_| Ok(Box::<App>::default()))).map_err(|e| e.to_string())
}
