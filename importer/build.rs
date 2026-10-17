// Packs resources/ and the staged Vita build (vita/tools/package_vita.sh) into the payload embedded in the importer.
use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};

use zip::write::SimpleFileOptions;
use zip::{CompressionMethod, ZipWriter};

fn add_dir(zip: &mut ZipWriter<fs::File>, dir: &Path, prefix: &str, skip: &[&str]) {
    let mut entries: Vec<_> = fs::read_dir(dir).unwrap().map(|e| e.unwrap().path()).collect();
    entries.sort();
    for path in entries {
        let name = path.file_name().unwrap().to_str().unwrap();
        if name == ".DS_Store" || skip.contains(&name) {
            continue;
        }
        let entry = format!("{prefix}{name}");
        if path.is_dir() {
            add_dir(zip, &path, &format!("{entry}/"), &[]);
        } else {
            let opts = SimpleFileOptions::default().compression_method(CompressionMethod::Deflated);
            zip.start_file(entry.as_str(), opts).unwrap();
            zip.write_all(&fs::read(&path).unwrap()).unwrap();
        }
    }
}

fn main() {
    let root = PathBuf::from(std::env::var("CARGO_MANIFEST_DIR").unwrap());
    let props = root.join("../game/Directory.Build.props");
    let version = fs::read_to_string(&props)
        .ok()
        .and_then(|s| Some(s.split_once("<GameVersion>")?.1.split_once("</GameVersion>")?.0.to_string()))
        .expect("GameVersion missing in game/Directory.Build.props");
    println!("cargo:rustc-env=LADX_GAME_VERSION={version}");
    println!("cargo:rerun-if-changed={}", props.display());

    let resources = root.join("resources");
    let stage = std::env::var("LADX_VITA_STAGE").map(PathBuf::from).unwrap_or_else(|_| root.join("../vita/build/vita/vpk"));
    println!("cargo:rerun-if-changed={}", resources.display());
    println!("cargo:rerun-if-changed={}", stage.display());
    println!("cargo:rerun-if-env-changed=LADX_VITA_STAGE");

    let out = PathBuf::from(std::env::var("OUT_DIR").unwrap()).join("payload.zip");
    let mut zip = ZipWriter::new(fs::File::create(out).unwrap());
    add_dir(&mut zip, &resources, "", &[]);
    if stage.join("eboot.bin").is_file() {
        add_dir(&mut zip, &stage, "vpk/", &["game.pak"]);
    } else {
        println!("cargo:warning=No Vita build in {}: the importer can only write game files (--tree)", stage.display());
    }
    zip.finish().unwrap();
}
