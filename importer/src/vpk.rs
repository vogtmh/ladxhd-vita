//! Turns the original game files into the Vita VPK, following the upstream patcher (Functions.PatchGameFiles).

use std::collections::{BTreeMap, BTreeSet, HashMap, HashSet};
use std::io::{Cursor, Read, Write};
use std::path::{Path, PathBuf};

use flate2::Compression;
use flate2::write::ZlibEncoder;
use md5::{Digest, Md5};
use zip::write::SimpleFileOptions;
use zip::{CompressionMethod, ZipArchive, ZipWriter};

use crate::artwork::{self, Choice};
use crate::{spinner, vcdiff};

// Built by build.rs from resources/ and the staged Vita build.
static PAYLOAD: &[u8] = include_bytes!(concat!(env!("OUT_DIR"), "/payload.zip"));
pub const GAME_VERSION: &str = env!("LADX_GAME_VERSION");

const MOD_DIRS: [&str; 8] = ["Animations", "Dungeon", "Graphics", "Languages", "LAHDMods", "Maps", "Music", "SoundEffects"];
// Deleted by the upstream patcher after patching (CleanUp.RemoveJunkMapFiles), together with "0 test map*".
const JUNK_FILES: [&str; 14] = [
    "Data/Dungeon/three_1.txt",
    "Data/Dungeon/three_2.txt",
    "Data/Dungeon/three_3.txt",
    "Data/Maps/cave bird.map.data",
    "Data/Maps/dungeon 7_2d.map.data",
    "Data/Maps/dungeon_end.map.data",
    "Data/Maps/dungeon3_1.map",
    "Data/Maps/dungeon3_1.map.data",
    "Data/Maps/dungeon3_2.map",
    "Data/Maps/dungeon3_2.map.data",
    "Data/Maps/dungeon3_3.map",
    "Data/Maps/dungeon3_3.map.data",
    "Data/Maps/dungeon3_4.map",
    "Data/Maps/dungeon3_4.map.data",
];

pub fn default_file_name() -> String {
    format!("LADXHD-Vita-v{GAME_VERSION}.vpk")
}

/// Accepts the game folder, a macOS app bundle from the upstream patcher, or a release download whose
/// subfolders hold the variants (DirectX, OpenGL, ...); every variant keeps the same original files.
pub fn find_game_root(dir: &Path) -> Option<PathBuf> {
    let is_root = |d: &Path| d.join("Content").is_dir() && d.join("Data").is_dir();
    let direct = [dir.to_path_buf(), dir.join("Contents/MacOS"), dir.join("Link's Awakening DX HD.app/Contents/MacOS")];
    if let Some(root) = direct.into_iter().find(|d| is_root(d)) {
        return Some(root);
    }
    let mut subdirs: Vec<PathBuf> = std::fs::read_dir(dir).ok()?.filter_map(|e| Some(e.ok()?.path())).collect();
    subdirs.sort();
    subdirs.into_iter().find(|d| is_root(d))
}

struct Payload {
    patches: HashMap<String, Vec<u8>>,
    targets: HashMap<String, Vec<String>>,
    manifest: Vec<(String, String)>,
    builtin: HashMap<String, Vec<u8>>,
    extra: Vec<(String, Vec<u8>)>,
    vpk: Vec<(String, Vec<u8>)>,
}

/// Game files keyed by lower-case path, like the case-insensitive file systems the patcher runs on.
#[derive(Default)]
struct Tree(BTreeMap<String, (String, Vec<u8>)>);

impl Tree {
    fn insert(&mut self, path: String, data: Vec<u8>) {
        self.0.insert(path.to_lowercase(), (path, data));
    }

    fn get(&self, path: &str) -> Result<&[u8], String> {
        self.0.get(&path.to_lowercase()).map(|(_, d)| d.as_slice()).ok_or_else(|| format!("{path} missing"))
    }
}

fn file_name(path: &str) -> &str {
    path.rsplit('/').next().unwrap()
}

fn parent(path: &str) -> &str {
    path.rsplit_once('/').map_or("", |(p, _)| p)
}

fn removed_after_patching(path: &str) -> bool {
    JUNK_FILES.iter().any(|j| j.eq_ignore_ascii_case(path)) || path.to_lowercase().starts_with("data/maps/0 test map")
}

fn is_chinese(name: &str) -> bool {
    let n = name.to_ascii_lowercase();
    let b = n.as_bytes();
    n.match_indices("chn").any(|(i, _)| (i == 0 || b[i - 1] == b'_') && matches!(b.get(i + 3), Some(b'.' | b'_')))
}

fn md5_hex(data: &[u8]) -> String {
    Md5::digest(data).iter().map(|b| format!("{b:02x}")).collect()
}

fn load_payload() -> Result<Payload, String> {
    let mut zip = ZipArchive::new(Cursor::new(PAYLOAD)).map_err(|e| e.to_string())?;
    let mut p = Payload {
        patches: HashMap::new(),
        targets: HashMap::new(),
        manifest: Vec::new(),
        builtin: HashMap::new(),
        extra: Vec::new(),
        vpk: Vec::new(),
    };
    for i in 0..zip.len() {
        let mut f = zip.by_index(i).map_err(|e| e.to_string())?;
        let name = f.name().map_err(|e| e.to_string())?.to_string();
        if name.ends_with('/') {
            continue;
        }
        let mut data = Vec::new();
        f.read_to_end(&mut data).map_err(|e| e.to_string())?;
        if let Some(n) = name.strip_prefix("patches/") {
            p.patches.insert(n.trim_end_matches(".vcdiff").to_lowercase(), data);
        } else if let Some(n) = name.strip_prefix("extra/") {
            p.extra.push((n.to_string(), data));
        } else if let Some(n) = name.strip_prefix("vpk/") {
            p.vpk.push((n.to_string(), data));
        } else if name == "d3map" {
            p.builtin.insert("dungeon3_1.map".into(), data);
        } else if name == "d3mapdata" {
            p.builtin.insert("dungeon3_1.map.data".into(), data);
        } else if name == "file_targets.txt" {
            for line in String::from_utf8_lossy(&data).lines().filter(|l| !l.starts_with('#')) {
                if let Some((src, derived)) = line.split_once('\t') {
                    p.targets.insert(src.to_lowercase(), derived.split(' ').map(String::from).collect());
                }
            }
        } else if name == "v100.md5" {
            for line in String::from_utf8_lossy(&data).lines() {
                if let Some((hash, path)) = line.split_once(' ') {
                    p.manifest.push((path.to_string(), hash.to_string()));
                }
            }
        }
    }
    Ok(p)
}

/// Collects the v1.0.0 files; installs patched to v1.1.4+ keep the originals of patched files in Data/Backup.
fn read_original(root: &Path, payload: &Payload, progress: &mut dyn FnMut(&str, f32)) -> Result<Vec<(String, Vec<u8>)>, String> {
    let backup = root.join("Data").join("Backup");
    let total = payload.manifest.len() as f32;
    let mut files = Vec::new();
    let mut bad = Vec::new();
    for (i, (rel, hash)) in payload.manifest.iter().enumerate() {
        progress("Checking game files", i as f32 / total);
        let name = file_name(rel);
        let key = name.to_lowercase();
        if removed_after_patching(rel) && !payload.targets.contains_key(&key) {
            continue;
        }
        let found = [root.join(rel), backup.join(name)]
            .into_iter()
            .filter_map(|p| std::fs::read(p).ok())
            .chain(payload.builtin.get(&key).cloned())
            .find(|d| md5_hex(d) == *hash);
        match found {
            Some(data) => files.push((rel.clone(), data)),
            None => bad.push(rel.as_str()),
        }
    }
    if !bad.is_empty() {
        return Err(format!(
            "{} game files are missing or modified, for example:\n  {}\n\n\
             Select the original v1.0.0 folder, or a game folder patched to v1.1.4 or newer (it keeps the \
             original files in Data/Backup).",
            bad.len(),
            bad[..bad.len().min(5)].join("\n  ")
        ));
    }
    Ok(files)
}

fn patch(payload: &Payload, originals: Vec<(String, Vec<u8>)>, progress: &mut dyn FnMut(&str, f32)) -> Result<Tree, String> {
    let step = format!("Patching to v{GAME_VERSION}");
    let derived: HashSet<String> = payload.targets.values().flatten().map(|n| n.to_lowercase()).collect();
    let apply = |src: &[u8], name: &str| -> Result<Option<Vec<u8>>, String> {
        match payload.patches.get(&name.to_lowercase()) {
            Some(p) => vcdiff::decode(src, p).map(Some).map_err(|e| format!("{name}: {e}")),
            None => Ok(None),
        }
    };
    let mut tree = Tree::default();
    for (rel, data) in &originals {
        tree.insert(rel.clone(), data.clone());
    }
    let total = originals.len() as f32;
    for (i, (rel, data)) in originals.iter().enumerate() {
        progress(&step, i as f32 / total);
        let name = file_name(rel);
        let key = name.to_lowercase();
        // Files that are created from a differently named file aren't patched in place.
        if derived.contains(&key) {
            continue;
        }
        for target in payload.targets.get(&key).into_iter().flatten() {
            if let Some(out) = apply(data, target)? {
                tree.insert(format!("{}/{target}", parent(rel)), out);
            }
        }
        if let Some(out) = apply(data, name)? {
            tree.insert(rel.clone(), out);
        }
    }
    tree.0.retain(|_, (path, _)| !removed_after_patching(path));
    for (rel, data) in &payload.extra {
        tree.insert(rel.clone(), data.clone());
    }
    Ok(tree)
}

/// LADXPAK2 pack read by rt::MountPack. Little endian: magic, u32 dirCount, u32 fileCount, dirs (u16 len, utf8),
/// files (u16 len, utf8, u64 offset, u64 size, u64 storedSize), data. storedSize < size means zlib.
fn make_pak(tree: &Tree, progress: &mut dyn FnMut(&str, f32)) -> Result<Vec<u8>, String> {
    let files: Vec<(&str, &[u8])> =
        tree.0.values().filter(|(p, _)| !is_chinese(file_name(p))).map(|(p, d)| (p.as_str(), d.as_slice())).collect();
    let mut dirs: BTreeSet<String> = ["Content", "Data", "Mods"].map(String::from).into();
    dirs.extend(MOD_DIRS.iter().map(|d| format!("Mods/{d}")));
    for (path, _) in &files {
        let mut p = parent(path);
        while !p.is_empty() {
            dirs.insert(p.to_string());
            p = parent(p);
        }
    }
    fn name(s: &str, out: &mut Vec<u8>) {
        out.extend_from_slice(&(s.len() as u16).to_le_bytes());
        out.extend_from_slice(s.as_bytes());
    }

    let mut blobs = Vec::with_capacity(files.len());
    for (i, (_, data)) in files.iter().enumerate() {
        progress("Packing game data", i as f32 / files.len() as f32);
        let mut z = ZlibEncoder::new(Vec::new(), Compression::best());
        z.write_all(data).map_err(|e| e.to_string())?;
        let packed = z.finish().map_err(|e| e.to_string())?;
        // Skip compression when it barely helps (PNGs): inflating costs load time on the Vita.
        blobs.push(if packed.len() * 10 < data.len() * 9 { packed } else { data.to_vec() });
    }

    let mut pak = b"LADXPAK2".to_vec();
    pak.extend_from_slice(&(dirs.len() as u32).to_le_bytes());
    pak.extend_from_slice(&(files.len() as u32).to_le_bytes());
    for d in &dirs {
        name(d, &mut pak);
    }
    let mut offset = (pak.len() + files.iter().map(|(p, _)| 2 + p.len() + 24).sum::<usize>()) as u64;
    for ((path, data), blob) in files.iter().zip(&blobs) {
        name(path, &mut pak);
        for v in [offset, data.len() as u64, blob.len() as u64] {
            pak.extend_from_slice(&v.to_le_bytes());
        }
        offset += blob.len() as u64;
    }
    for blob in blobs {
        pak.extend_from_slice(&blob);
    }
    Ok(pak)
}

fn write_vpk(out: &Path, pak: &[u8], vpk: &[(String, Vec<u8>)]) -> Result<(), String> {
    let file = std::fs::File::create(out).map_err(|e| format!("{}: {e}", out.display()))?;
    let mut zip = ZipWriter::new(file);
    let err = |e: zip::result::ZipError| e.to_string();
    // Stored so installing is a plain copy instead of a slow inflate on the Vita.
    zip.start_file("game.pak", SimpleFileOptions::default().compression_method(CompressionMethod::Stored)).map_err(err)?;
    zip.write_all(pak).map_err(|e| e.to_string())?;
    let deflate = SimpleFileOptions::default().compression_method(CompressionMethod::Deflated).compression_level(Some(9));
    for (name, data) in vpk {
        zip.start_file(name.as_str(), deflate).map_err(err)?;
        zip.write_all(data).map_err(|e| e.to_string())?;
    }
    zip.finish().map_err(err)?;
    Ok(())
}

fn write_tree(dir: &Path, tree: &Tree) -> Result<(), String> {
    for (path, data) in tree.0.values() {
        let dest = dir.join(path);
        std::fs::create_dir_all(dest.parent().unwrap()).map_err(|e| e.to_string())?;
        std::fs::write(&dest, data).map_err(|e| format!("{}: {e}", dest.display()))?;
    }
    for d in MOD_DIRS {
        std::fs::create_dir_all(dir.join("Mods").join(d)).map_err(|e| e.to_string())?;
    }
    Ok(())
}

/// The patched intro sprite sheet and atlas, for artwork previews without a full import.
pub fn intro_files(root: &Path) -> Result<(Vec<u8>, Vec<u8>), String> {
    let payload = load_payload()?;
    let get = |rel: &str| -> Result<Vec<u8>, String> {
        let hash = payload.manifest.iter().find(|(p, _)| p == rel).map(|(_, h)| h).ok_or(format!("{rel} unknown"))?;
        let name = file_name(rel);
        let data = [root.join(rel), root.join("Data").join("Backup").join(name)]
            .into_iter()
            .filter_map(|p| std::fs::read(p).ok())
            .find(|d| md5_hex(d) == *hash)
            .ok_or(format!("{rel} is missing or modified"))?;
        match payload.patches.get(&name.to_lowercase()) {
            Some(p) => vcdiff::decode(&data, p).map_err(|e| format!("{name}: {e}")),
            None => Ok(data),
        }
    };
    Ok((get("Data/Intro/intro.png")?, get("Data/Intro/intro.atlas")?))
}

/// Returns notes for the player (e.g. artwork that had to fall back).
pub fn build(
    input: &Path,
    output: Option<&Path>,
    tree_dir: Option<&Path>,
    artwork: &Choice,
    progress: &mut dyn FnMut(&str, f32),
) -> Result<Vec<String>, String> {
    let root = find_game_root(input).ok_or_else(|| format!("{} doesn't contain the game's Content and Data folders", input.display()))?;
    let payload = load_payload()?;
    if output.is_some() && payload.vpk.is_empty() {
        return Err("This importer was built without the Vita files (run vita/tools/package_vita.sh before building it).".into());
    }
    let originals = read_original(&root, &payload, progress)?;
    let tree = patch(&payload, originals, progress)?;
    if let Some(dir) = tree_dir {
        progress("Writing game files", 1.0);
        write_tree(dir, &tree)?;
    }
    let mut notes = Vec::new();
    if let Some(out) = output {
        let pak = make_pak(&tree, progress)?;
        progress("Preparing artwork", 1.0);
        let mut vpk = payload.vpk;
        vpk.push(("ui/spinner.bin".into(), spinner::make(tree.get("Data/ui.png")?, tree.get("Data/ui.atlas")?)?));
        let art = artwork::build_all(artwork, tree.get("Data/Intro/intro.png")?, tree.get("Data/Intro/intro.atlas")?, &mut |n| notes.push(n))?;
        // Generated files replace any left in an older staged Vita build.
        vpk.retain(|(name, _)| !art.iter().any(|(n, _)| n == name));
        vpk.extend(art);
        progress("Writing VPK", 1.0);
        write_vpk(out, &pak, &vpk)?;
    }
    Ok(notes)
}
