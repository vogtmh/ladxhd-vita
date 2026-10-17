//! RFC 3284 VCDIFF decoder, including the open-vcdiff extensions written by the .NET VCDiff library
//! (version byte 'S', interleaved sections, optional Adler-32 per window).

const NEAR: usize = 4;
const SAME: usize = 3 * 256;

#[derive(Clone, Copy)]
enum Op {
    Noop,
    Add(u8),
    Run(u8),
    Copy(u8, u8),
}

fn code_table() -> Vec<[Op; 2]> {
    use Op::*;
    let mut t = vec![[Run(0), Noop], [Add(0), Noop]];
    t.extend((1..=17).map(|s| [Add(s), Noop]));
    for m in 0..9 {
        t.push([Copy(0, m), Noop]);
        t.extend((4..=18).map(|s| [Copy(s, m), Noop]));
    }
    for m in 0..6 {
        for a in 1..=4 {
            t.extend((4..=6).map(|c| [Add(a), Copy(c, m)]));
        }
    }
    for m in 6..9 {
        t.extend((1..=4).map(|a| [Add(a), Copy(4, m)]));
    }
    t.extend((0..9).map(|m| [Copy(4, m), Add(1)]));
    debug_assert_eq!(t.len(), 256);
    t
}

struct Reader<'a> {
    d: &'a [u8],
    p: usize,
}

impl<'a> Reader<'a> {
    fn byte(&mut self) -> Result<u8, String> {
        let b = *self.d.get(self.p).ok_or("unexpected end of patch")?;
        self.p += 1;
        Ok(b)
    }

    fn bytes(&mut self, n: usize) -> Result<&'a [u8], String> {
        let end = self.p.checked_add(n).filter(|&e| e <= self.d.len()).ok_or("unexpected end of patch")?;
        let s = &self.d[self.p..end];
        self.p = end;
        Ok(s)
    }

    fn varint(&mut self) -> Result<usize, String> {
        let mut v: u64 = 0;
        for _ in 0..10 {
            let b = self.byte()?;
            v = (v << 7) | u64::from(b & 0x7f);
            if b & 0x80 == 0 {
                return usize::try_from(v).map_err(|_| "integer too large".into());
            }
        }
        Err("bad integer in patch".into())
    }

    fn at_end(&self) -> bool {
        self.p >= self.d.len()
    }
}

struct AddrCache {
    near: [usize; NEAR],
    next: usize,
    same: [usize; SAME],
}

impl AddrCache {
    fn new() -> Self {
        AddrCache { near: [0; NEAR], next: 0, same: [0; SAME] }
    }

    fn decode(&mut self, here: usize, mode: u8, r: &mut Reader) -> Result<usize, String> {
        let addr = match mode as usize {
            0 => r.varint()?,
            1 => here.checked_sub(r.varint()?).ok_or("bad HERE address")?,
            m @ 2..=5 => self.near[m - 2] + r.varint()?,
            m => self.same[(m - 6) * 256 + r.byte()? as usize],
        };
        self.near[self.next] = addr;
        self.next = (self.next + 1) % NEAR;
        self.same[addr % SAME] = addr;
        Ok(addr)
    }
}

fn adler32(data: &[u8]) -> u32 {
    let (mut a, mut b) = (1u32, 0u32);
    for chunk in data.chunks(5552) {
        for &x in chunk {
            a += u32::from(x);
            b += a;
        }
        a %= 65521;
        b %= 65521;
    }
    (b << 16) | a
}

pub fn decode(source: &[u8], patch: &[u8]) -> Result<Vec<u8>, String> {
    let table = code_table();
    let mut r = Reader { d: patch, p: 0 };
    if r.bytes(3)? != [0xd6, 0xc3, 0xc4] {
        return Err("not a VCDIFF file".into());
    }
    let version = r.byte()?;
    if version != 0 && version != b'S' {
        return Err(format!("unsupported VCDIFF version {version:#x}"));
    }
    let hdr = r.byte()?;
    if hdr & 0x03 != 0 {
        return Err("secondary compression / custom code tables are not supported".into());
    }
    if hdr & 0x04 != 0 {
        let n = r.varint()?;
        r.bytes(n)?;
    }

    let mut out: Vec<u8> = Vec::new();
    while !r.at_end() {
        let win = r.byte()?;
        let segment = if win & 0x03 != 0 {
            let len = r.varint()?;
            let pos = r.varint()?;
            let base: &[u8] = if win & 0x01 != 0 { source } else { &out };
            let end = pos.checked_add(len).filter(|&e| e <= base.len()).ok_or("source segment out of range")?;
            base[pos..end].to_vec()
        } else {
            Vec::new()
        };
        let delta_len = r.varint()?;
        let delta_start = r.p;
        let target_len = r.varint()?;
        if r.byte()? != 0 {
            return Err("compressed delta sections are not supported".into());
        }
        let data_len = r.varint()?;
        let inst_len = r.varint()?;
        let addr_len = r.varint()?;
        let checksum = if win & 0x04 != 0 { Some(u32::from_be_bytes(r.bytes(4)?.try_into().unwrap())) } else { None };
        let mut data = Reader { d: r.bytes(data_len)?, p: 0 };
        let mut inst = Reader { d: r.bytes(inst_len)?, p: 0 };
        let mut addr = Reader { d: r.bytes(addr_len)?, p: 0 };
        if r.p != delta_start + delta_len {
            return Err("window length mismatch".into());
        }
        // Interleaved windows keep sizes, data and addresses inline in the instruction section.
        let interleaved = data_len == 0 && addr_len == 0;

        let src = &segment[..];
        let mut t: Vec<u8> = Vec::with_capacity(target_len);
        let mut cache = AddrCache::new();
        while !inst.at_end() {
            for op in table[inst.byte()? as usize] {
                let size = match op {
                    Op::Noop => continue,
                    Op::Add(s) | Op::Run(s) | Op::Copy(s, _) if s != 0 => s as usize,
                    _ => inst.varint()?,
                };
                match op {
                    Op::Add(_) => {
                        let bytes = if interleaved { inst.bytes(size)? } else { data.bytes(size)? };
                        t.extend_from_slice(bytes);
                    }
                    Op::Run(_) => {
                        let b = if interleaved { inst.byte()? } else { data.byte()? };
                        t.resize(t.len() + size, b);
                    }
                    Op::Copy(_, mode) => {
                        let here = src.len() + t.len();
                        let a = cache.decode(here, mode, if interleaved { &mut inst } else { &mut addr })?;
                        if a >= here {
                            return Err("COPY address out of range".into());
                        }
                        if a + size <= src.len() {
                            t.extend_from_slice(&src[a..a + size]);
                        } else {
                            // Target copies may overlap the bytes they produce, so copy one at a time.
                            for i in a..a + size {
                                let b = if i < src.len() { src[i] } else { t[i - src.len()] };
                                t.push(b);
                            }
                        }
                    }
                    Op::Noop => unreachable!(),
                }
            }
        }
        if t.len() != target_len {
            return Err("decoded window has the wrong size".into());
        }
        if let Some(sum) = checksum {
            if adler32(&t) != sum {
                return Err("checksum mismatch".into());
            }
        }
        out.extend_from_slice(&t);
    }
    Ok(out)
}
