// Asset importer: builds the PS Vita VPK from the user's own copy of Link's Awakening DX HD.
// A console program on Windows so command-line output works; a double-click closes its console.

mod artwork;
mod gui;
mod spinner;
mod vcdiff;
mod vpk;

use std::path::PathBuf;
use std::process::ExitCode;

use artwork::{Choice, GatePos, Slot, Source};

const USAGE: &str = "usage: ladxhd-vita-importer [<game folder> [-o <file.vpk>] [--tree <folder>]\n\
                     \x20                           [--bubble <img>] [--background <img>] [--startup <img>]\n\
                     \x20                           [--button centre|right] [--bg-position <0..1>]]\n\
                     Without arguments the window opens. The game folder is the original v1.0.0 release\n\
                     or a copy already patched to v1.1.4 or newer. --tree also writes the patched game files.\n\
                     <img> is an image file or an https:// image link; default: chosen SteamGridDB\n\
                     images, or artwork from the game's own files when offline. --button places the LiveArea\n\
                     start button, --bg-position the background crop (default: keeps the egg visible).";

fn cli(args: Vec<String>) -> Result<(), String> {
    let mut args = args.into_iter();
    let mut input = None;
    let mut output = None;
    let mut tree = None;
    let mut art = Choice::default();
    while let Some(a) = args.next() {
        let slot = match a.as_str() {
            "--bubble" => Some(Slot::Bubble),
            "--background" => Some(Slot::Background),
            "--startup" => Some(Slot::Startup),
            _ => None,
        };
        if let Some(slot) = slot {
            let v = args.next().ok_or(format!("{a} needs an image file or link"))?;
            art.set(slot, if v.starts_with("https://") { Source::Url(v) } else { Source::File(v.into()) });
            continue;
        }
        match a.as_str() {
            "-o" => output = Some(PathBuf::from(args.next().ok_or("-o needs a file name")?)),
            "--tree" => tree = Some(PathBuf::from(args.next().ok_or("--tree needs a folder")?)),
            "--button" => {
                art.gate = match args.next().as_deref() {
                    Some("centre" | "center") => GatePos::Center,
                    Some("right") => GatePos::Right,
                    _ => return Err("--button needs centre or right".into()),
                }
            }
            "--bg-position" => {
                let v: f32 = args.next().and_then(|v| v.parse().ok()).ok_or("--bg-position needs a number from 0 to 1")?;
                art.bg_focus = Some(v.clamp(0.0, 1.0));
            }
            "-h" | "--help" => return Err(USAGE.into()),
            _ if input.is_none() => input = Some(PathBuf::from(a)),
            _ => return Err(format!("unexpected argument: {a}\n{USAGE}")),
        }
    }
    let input = input.ok_or(USAGE)?;
    let output = output.or_else(|| tree.is_none().then(|| PathBuf::from(vpk::default_file_name())));
    let mut last = String::new();
    let notes = vpk::build(&input, output.as_deref(), tree.as_deref(), &art, &mut |step: &str, _| {
        if step != last {
            println!("{step}");
            last = step.to_string();
        }
    })?;
    for n in notes {
        println!("note: {n}");
    }
    Ok(())
}

/// The window has no console on Windows, so startup failures are shown in a message box.
fn show_error(text: &str) {
    rfd::MessageDialog::new()
        .set_level(rfd::MessageLevel::Error)
        .set_title("LADXHD Vita Importer")
        .set_description(text)
        .show();
}

/// Closes the console Windows opens when the importer is started by double-click; a console shared with a
/// command prompt stays for the command-line output.
#[cfg(windows)]
fn close_own_console() {
    unsafe extern "system" {
        fn GetConsoleProcessList(list: *mut u32, count: u32) -> u32;
        fn FreeConsole() -> i32;
    }
    let mut pids = [0u32; 2];
    if unsafe { GetConsoleProcessList(pids.as_mut_ptr(), 2) } == 1 {
        unsafe { FreeConsole() };
    }
}

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let result = if args.is_empty() {
        #[cfg(windows)]
        close_own_console();
        std::panic::set_hook(Box::new(|info| show_error(&format!("The importer crashed:\n{info}"))));
        gui::run().inspect_err(|e| show_error(&format!("The window could not be opened:\n{e}")))
    } else {
        cli(args)
    };
    match result {
        Ok(()) => ExitCode::SUCCESS,
        Err(e) => {
            eprintln!("error: {e}");
            ExitCode::FAILURE
        }
    }
}
